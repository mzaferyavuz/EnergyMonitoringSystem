using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnergyMonitoringSystem.Data;
using Entities = EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NModbus;

namespace EnergyMonitoringSystem.Service.BackgroundServices
{
    public class ModbusCollectorWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _period = TimeSpan.FromMinutes(15);

        public ModbusCollectorWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
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

                // Sadece aktif olan cihazları getir
                var devices = await dbContext.ModbusDevices.Where(d => d.IsActive).ToListAsync();

                foreach (var device in devices)
                {
                    if (device == null) continue;
                    try
                    {
                        await ReadDeviceData(device, dbContext);
                    }
                    catch (Exception ex)
                    {
                        // Hata yönetimi: Loglama yapılabilir
                        Console.WriteLine($"Cihaz okuma hatası ({device.DeviceName}): {ex.Message}");
                    }
                }

                // Tüm okumalar bittikten sonra veritabanına toplu kaydet
                await dbContext.SaveChangesAsync();
            }
        }

        private async Task ReadDeviceData(Entities.ModbusDevice device, AppDbContext dbContext)
        {
            using (TcpClient client = new TcpClient())
            {
                // Timeout kontrolü ile bağlantı
                var connectTask = client.ConnectAsync(device.IpAddress, device.Port);
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                {
                    throw new TimeoutException("Cihaza bağlanılamadı (Timeout).");
                }

                var factory = new ModbusFactory();
                IModbusMaster master = factory.CreateMaster(client);

                var registers = await dbContext.ModbusRegisters
                    .Where(r => r.ModbusDeviceId == device.Id)
                    .ToListAsync();

                var physicalMeters = await dbContext.Meters
                     .Include(m => m.ChildMeters)
                     .Where(m => m.ModbusDeviceId == device.Id && !m.IsVirtual)
                     .ToListAsync();

                foreach (var reg in registers)
                {
                    // --- GELİŞTİRİLEN KISIM BAŞLANGICI ---

                    // 1. Veri tipine göre kaç register okunacağını belirle
                    ushort pointsToRead = 1;
                    if (reg.DataType == "Float" || reg.DataType == "Int32") pointsToRead = 2; // 32-bit veriler 2 register kaplar

                    // 2. Modbus'tan ham veriyi oku (ushort dizisi döner)
                    ushort[] inputs = await master.ReadHoldingRegistersAsync(device.UnitId, (ushort)reg.RegisterAddress, pointsToRead);

                    // 3. Ham veriyi gerçek sayıya dönüştür
                    double rawValue = 0;

                    if (reg.DataType == "Float" && inputs.Length >= 2)
                    {
                        // Modbus'ta genellikle LowWord-HighWord veya tam tersi olabilir. 
                        // Standart IEEE 754 Float dönüşümü:
                        byte[] bytes = new byte[4];
                        // Endianness (Byte sıralaması) cihaza göre değişebilir, burada standart birleşim yapıyoruz:
                        byte[] low = BitConverter.GetBytes(inputs[0]);
                        byte[] high = BitConverter.GetBytes(inputs[1]);

                        // Örnek birleşim (Cihazın dokümanına göre low/high yer değiştirebilir)
                        bytes[0] = low[0]; bytes[1] = low[1];
                        bytes[2] = high[0]; bytes[3] = high[1];

                        rawValue = BitConverter.ToSingle(bytes, 0);
                    }
                    else if (reg.DataType == "Int32" && inputs.Length >= 2)
                    {
                        // 32-bit Integer dönüşümü
                        int val = inputs[0] | (inputs[1] << 16);
                        rawValue = val;
                    }
                    else
                    {
                        // Varsayılan 16-bit okuma (Mevcut kodun)
                        rawValue = inputs[0];
                    }

                    // Çarpan (Scale Factor) uygula
                    double finalValue = rawValue * reg.ScaleFactor;

                    // --- GELİŞTİRİLEN KISIM BİTİŞİ ---

                    // Buradan sonrası senin mevcut kodunla aynı...
                    foreach (var meter in physicalMeters)
                    {
                        var historyEntry = new Entities.MeterHistory
                        {
                            MeterId = meter.Id,
                            Timestamp = DateTime.Now,
                            Value = finalValue,
                            Type = reg.Type
                        };
                        dbContext.MeterHistories.Add(historyEntry);

                        // Sanal sayaç mantığı (Aynen kalacak)...
                        if (reg.Type == RegisterType.kWh && meter.ChildMeters != null)
                        {
                            // ...
                        }
                    }
                }
            }
        }
    }
}