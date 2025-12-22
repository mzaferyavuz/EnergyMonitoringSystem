using EnergyMonitoringSystem.Core.Helpers;
using EnergyMonitoringSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NModbus;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Entities = EnergyMonitoringSystem.Core.Entities;

namespace EnergyMonitoringSystem.Service.BackgroundServices
{
    public class ModbusCollectorWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _period = TimeSpan.FromMinutes(15);
        private readonly ILogger<ModbusCollectorWorker> _logger; // Logger tanımlandı

        public ModbusCollectorWorker(IServiceProvider serviceProvider, ILogger<ModbusCollectorWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Modbus Veri Toplama Servisi başlatıldı.");
            while (!stoppingToken.IsCancellationRequested)
            {
                await ReadAllDevicesAsync();
                await Task.Delay(_period, stoppingToken);
            }
        }

        private async Task ReadAllDevicesAsync()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 1. Aktif Gateway/Cihazları Getir
                var devices = await dbContext.ModbusDevices.Where(d => d.IsActive).ToListAsync();

                foreach (var device in devices)
                {
                    // RETRY LOGIC (Tekrar Deneme Mekanizması)
                    // Cihaza bağlanamazsa 3 kereye kadar tekrar dener
                    int retryCount = 0;
                    bool success = false;
                    while (retryCount < 3 && !success)
                    {
                        try
                        {
                            await ReadDeviceData(device, dbContext);
                            success = true; // Başarılı olursa döngüden çık
                        }
                        catch (Exception ex)
                        {
                            retryCount++;
                            _logger.LogWarning(ex, "Cihaz okuma hatası ({DeviceName}). Deneme: {Count}/3", device.DeviceName, retryCount);
                            // 2 saniye bekle tekrar dene
                            if (retryCount < 3) await Task.Delay(2000);
                            else _logger.LogError(ex, "Cihaz okuma BAŞARISIZ OLDU ({DeviceName}).", device.DeviceName);
                        }
                    }
                }

                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Tüm cihaz okumaları tamamlandı ve kaydedildi.");
            }
        }

        private async Task ReadDeviceData(Entities.ModbusDevice device, AppDbContext dbContext)
        {
            using (TcpClient client = new TcpClient())
            {
                // Bağlantı (Timeout: 3sn)
                var connectTask = client.ConnectAsync(device.IpAddress, device.Port);
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                {
                    throw new TimeoutException("Cihaza bağlanılamadı (Timeout).");
                }

                var factory = new ModbusFactory();
                IModbusMaster master = factory.CreateMaster(client);

                // 2. Bu cihaza (Gateway'e) bağlı tüm fiziksel sayaçları bul
                var meters = await dbContext.Meters
                    .Include(m => m.ChildMeters) // Sanal sayaçlar için
                    .Where(m => m.ModbusDeviceId == device.Id && !m.IsVirtual)
                    .ToListAsync();

                foreach (var meter in meters)
                {
                    // 3. Her sayacın kendine özel register tanımlarını getir
                    var registers = await dbContext.ModbusRegisters
                        .Include(r => r.MeasurementParameter)
                        .Where(r => r.MeterId == meter.Id)
                        .ToListAsync();

                    foreach (var reg in registers)
                    {
                        try
                        {
                            // Okunacak register sayısını belirle
                            ushort pointsToRead = 1;
                            if (reg.DataType == "Float" || reg.DataType == "Int32") pointsToRead = 2;

                            // Modbus Okuma (Dikkat: UnitId cihazdan geliyor. Eğer her sayaç farklı UnitId ise Meter tablosuna UnitId eklenmeli. Şimdilik Device'dan alıyoruz)
                            ushort[] inputs = await master.ReadHoldingRegistersAsync(device.UnitId, (ushort)reg.RegisterAddress, pointsToRead);

                            // Byte Dönüşümü ve Hesaplama
                            double rawValue = ModbusHelper.ConvertModbusData(inputs, reg.DataType, reg.ByteOrder);
                            double finalValue = rawValue * reg.ScaleFactor;

                            // Kayıt
                            var historyEntry = new Entities.MeterHistory
                            {
                                MeterId = meter.Id,
                                Timestamp = DateTime.Now,
                                Value = finalValue,
                                MeasurementParameterId = reg.MeasurementParameterId
                            };
                            dbContext.MeterHistories.Add(historyEntry);

                            // Sanal Sayaç Dağıtımı (Sadece Aktif Enerji için)
                            if (reg.MeasurementParameter != null &&
                                reg.MeasurementParameter.Key == "ActiveEnergy" &&
                                meter.ChildMeters != null)
                            {
                                var virtualChildren = meter.ChildMeters.Where(c => c.IsVirtual);
                                foreach (var vMeter in virtualChildren)
                                {
                                    dbContext.MeterHistories.Add(new Entities.MeterHistory
                                    {
                                        MeterId = vMeter.Id,
                                        Timestamp = DateTime.Now,
                                        Value = finalValue * vMeter.VirtualMultiplier,
                                        MeasurementParameterId = reg.MeasurementParameterId
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Register bazlı hatayı logla ama tüm cihazı durdurma
                            _logger.LogError(ex, "Register Okuma Hatası! Meter: {MeterName}, Address: {Address}", meter.Name, reg.RegisterAddress);
                        }
                    }
                }
            }
        }

        private double ConvertModbusData(ushort[] inputs, string dataType, string byteOrder)
        {
            if (inputs.Length < 1) return 0;

            byte[] bytes = new byte[inputs.Length * 2];

            // Registerları byte dizisine döküyoruz
            for (int i = 0; i < inputs.Length; i++)
            {
                byte[] b = BitConverter.GetBytes(inputs[i]);
                bytes[i * 2] = b[0];
                bytes[i * 2 + 1] = b[1];
            }

            // --- ENDIANNESS YÖNETİMİ ---
            // Modbus standart olarak Big-Endian (Word bazlı) gönderir ama Byte'lar bazen terstir.
            // inputs[0] = High Word, inputs[1] = Low Word (veya tam tersi)

            byte[] finalBytes = new byte[4]; // Max 32 bit destekliyoruz şimdilik

            if (dataType == "Float" || dataType == "Int32")
            {
                if (inputs.Length < 2) return 0;

                // Cihazdan gelen ushort'ları byte'a çevirelim
                byte[] w1 = BitConverter.GetBytes(inputs[0]);
                byte[] w2 = BitConverter.GetBytes(inputs[1]);

                // Senaryolar (ABCD, CDAB, BADC, DCBA)
                switch (byteOrder)
                {
                    case "BigEndian": // ABCD (High Word First, Big Endian Bytes) -> C# Little Endian ister, biz dizeriz
                        // Gelen: [A,B] [C,D] -> Bizim istediğimiz (Little Endian): D, C, B, A
                        finalBytes[0] = w2[0]; finalBytes[1] = w2[1]; // Low Word
                        finalBytes[2] = w1[0]; finalBytes[3] = w1[1]; // High Word
                        break;

                    case "LittleEndian": // CDAB (Low Word First)
                        // Gelen: [C,D] [A,B] -> İstediğimiz: B, A, D, C (veya tam tersi cihaza göre)
                        // Genellikle Modbus'ta LittleEndian dendiğinde Word Swap kastedilir.
                        finalBytes[0] = w1[0]; finalBytes[1] = w1[1];
                        finalBytes[2] = w2[0]; finalBytes[3] = w2[1];
                        break;

                    // Diğer karmaşık durumlar (Byte Swap) gerekirse buraya eklenir
                    default:
                        // Varsayılan BigEndian davranışı
                        finalBytes[0] = w2[0]; finalBytes[1] = w2[1];
                        finalBytes[2] = w1[0]; finalBytes[3] = w1[1];
                        break;
                }

                if (dataType == "Float") return BitConverter.ToSingle(finalBytes, 0);
                if (dataType == "Int32") return BitConverter.ToInt32(finalBytes, 0);
            }
            else
            {
                // 16-bit
                return inputs[0];
            }

            return 0;
        }
    }
}