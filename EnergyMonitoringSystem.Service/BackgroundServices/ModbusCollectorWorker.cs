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
            using (TcpClient client = new TcpClient(device.IpAddress, device.Port))
            {
                var factory = new ModbusFactory();
                // NModbus 3.x sürümü için CreateMaster kullanılır
                IModbusMaster master = factory.CreateMaster(client);

                // Bu cihaza bağlı fiziksel sayaçları bul (Sanal olmayanlar)
                var physicalMeters = await dbContext.Meters
                    .Include(m => m.ChildMeters)
                    .Where(m => m.ModbusDeviceId == device.Id && !m.IsVirtual)
                    .ToListAsync();

                // Cihaza tanımlı register adreslerini getir
                var registers = await dbContext.ModbusRegisters
                    .Where(r => r.ModbusDeviceId == device.Id)
                    .ToListAsync();

                foreach (var reg in registers)
                {
                    // Modbus üzerinden veriyi oku
                    ushort[] inputs = await master.ReadHoldingRegistersAsync(device.UnitId, (ushort)reg.RegisterAddress, 1);
                    double rawValue = inputs[0] * reg.ScaleFactor;

                    foreach (var meter in physicalMeters)
                    {
                        // 1. Fiziksel sayacın okumasını kaydet
                        var historyEntry = new Entities.MeterHistory
                        {
                            MeterId = meter.Id,
                            Timestamp = DateTime.Now,
                            Value = rawValue,
                            Type = reg.Type
                        };
                        dbContext.MeterHistories.Add(historyEntry);

                        // 2. Madde 3 & 4: Sanal sayaçlara veri dağıtımı
                        // Genellikle sanal sayaçlar sadece kWh (enerji) üzerinden bölünür
                        if (reg.Type == RegisterType.kWh && meter.ChildMeters != null)
                        {
                            var virtualChildren = meter.ChildMeters.Where(c => c.IsVirtual);
                            foreach (var vMeter in virtualChildren)
                            {
                                dbContext.MeterHistories.Add(new Entities.MeterHistory
                                {
                                    MeterId = vMeter.Id,
                                    Timestamp = DateTime.Now,
                                    Value = rawValue * vMeter.VirtualMultiplier,
                                    Type = RegisterType.kWh
                                });
                            }
                        }
                    }
                }
            }
        }
    }
}