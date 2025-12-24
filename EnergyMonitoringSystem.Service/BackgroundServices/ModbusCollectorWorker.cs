using EnergyMonitoringSystem.Data;
using EnergyMonitoringSystem.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NModbus;
using System.Net.Sockets;

namespace EnergyMonitoringSystem.Service.BackgroundServices
{
    public class ModbusCollectorWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ModbusCollectorWorker> _logger;
        private readonly TimeSpan _period = TimeSpan.FromMinutes(1); // Test için 1 dakikaya düşürdüm

        public ModbusCollectorWorker(IServiceScopeFactory serviceScopeFactory, ILogger<ModbusCollectorWorker> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Modbus Veri Toplama Servisi başlatıldı.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAllGatewaysAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Genel döngü hatası.");
                }

                await Task.Delay(_period, stoppingToken);
            }
        }

        private async Task ProcessAllGatewaysAsync(CancellationToken ct)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 1. Tüm aktif cihazları (Gatewayleri) IP'ye göre grupla
                // Bu sayede aynı IP'ye sahip cihazlar için tek bağlantı açacağız
                var activeDevices = await dbContext.ModbusDevices
                    .Where(d => d.IsActive)
                    .ToListAsync();

                var gatewayGroups = activeDevices.GroupBy(d => d.IpAddress);

                foreach (var gatewayGroup in gatewayGroups)
                {
                    string ipAddress = gatewayGroup.Key;
                    int port = gatewayGroup.First().Port; // Gruptaki ilk cihazın portunu al

                    try
                    {
                        using (TcpClient client = new TcpClient())
                        {
                            // Gateway'e bağlan (Timeout 3sn)
                            var connectTask = client.ConnectAsync(ipAddress, port);
                            if (await Task.WhenAny(connectTask, Task.Delay(3000, ct)) != connectTask)
                                throw new TimeoutException($"{ipAddress} adresine bağlanılamadı.");

                            var factory = new ModbusFactory();
                            IModbusMaster master = factory.CreateMaster(client);
                            master.Transport.Retries = 2;
                            master.Transport.ReadTimeout = 2000;

                            // 2. Bu IP adresine (Gateway) bağlı tüm cihazları (ModbusDevice satırlarını) dön
                            foreach (var deviceRecord in gatewayGroup)
                            {
                                // 3. Bu cihaz tanımına bağlı gerçek sayaçları (Meter) getir
                                var meters = await dbContext.Meters
                                    .Include(m => m.ModbusRegisters)
                                        .ThenInclude(r => r.MeasurementParameter)
                                    .Include(m => m.ChildMeters)
                                    .Where(m => m.ModbusDeviceId == deviceRecord.Id)
                                    .ToListAsync();

                                foreach (var meter in meters)
                                {
                                    _logger.LogInformation($"Okunuyor: {meter.Name} (Slave: {deviceRecord.UnitId})");

                                    foreach (var reg in meter.ModbusRegisters)
                                    {
                                        try
                                        {
                                            // 4. Veriyi Oku
                                            ushort points = (ushort)(reg.DataType == "Float" || reg.DataType == "Int32" ? 2 : 1);
                                            // SlaveID olarak deviceRecord içindeki UnitId'yi kullanıyoruz
                                            ushort[] inputs = await master.ReadHoldingRegistersAsync((byte)deviceRecord.UnitId, (ushort)reg.RegisterAddress, points);

                                            // 5. Veriyi Çevir (Dahili metodumuzu kullanıyoruz)
                                            double rawValue = ConvertRawData(inputs, reg.DataType, reg.ByteOrder);
                                            double finalValue = rawValue * reg.ScaleFactor;

                                            // 6. Veritabanına Kaydet
                                            var history = new MeterHistory
                                            {
                                                MeterId = meter.Id,
                                                Timestamp = DateTime.Now,
                                                Value = finalValue,
                                                MeasurementParameterId = reg.MeasurementParameterId
                                            };
                                            dbContext.MeterHistories.Add(history);

                                            // 7. Sanal Sayaç Varsa Dağıt (Örn: %10'u bu sayaca aittir gibi)
                                            if (meter.ChildMeters != null)
                                            {
                                                foreach (var vChild in meter.ChildMeters.Where(c => c.IsVirtual))
                                                {
                                                    dbContext.MeterHistories.Add(new MeterHistory
                                                    {
                                                        MeterId = vChild.Id,
                                                        Timestamp = history.Timestamp,
                                                        Value = finalValue * vChild.VirtualMultiplier,
                                                        MeasurementParameterId = reg.MeasurementParameterId
                                                    });
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning($"Register hatası ({reg.RegisterAddress}): {ex.Message}");
                                        }

                                        // Gateway ve seri hattın dinlenmesi için çok kısa bir bekleme
                                        await Task.Delay(50, ct);
                                    }
                                }
                            }
                        }
                        // Gateway bazlı değişiklikleri kaydet
                        await dbContext.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"{ipAddress} Gateway hatası: {ex.Message}");
                    }
                }
            }
        }

        // Kendi iç dönüşüm metodumuz (Hata almamak için buraya aldım)
        private double ConvertRawData(ushort[] inputs, string dataType, string byteOrder)
        {
            if (inputs == null || inputs.Length == 0) return 0;

            if (dataType == "Float" || dataType == "Int32")
            {
                if (inputs.Length < 2) return 0;

                byte[] high = BitConverter.GetBytes(inputs[0]);
                byte[] low = BitConverter.GetBytes(inputs[1]);
                byte[] full = new byte[4];

                // Byte Order Mantığı (Gatewaylerde genellikle BigEndian istenir)
                if (byteOrder == "BigEndian") // ABCD
                {
                    full[0] = low[1]; full[1] = low[0]; full[2] = high[1]; full[3] = high[0];
                }
                else // LittleEndian - CDAB (Word Swap)
                {
                    full[0] = high[1]; full[1] = high[0]; full[2] = low[1]; full[3] = low[0];
                }

                if (dataType == "Float") return BitConverter.ToSingle(full, 0);
                return BitConverter.ToInt32(full, 0);
            }

            return inputs[0]; // 16 bit
        }
    }
}