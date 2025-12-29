using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Core.Helpers;
using EnergyMonitoringSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NModbus;
using System.Net.Sockets;

namespace EnergyMonitoringSystem.Service.BackgroundServices
{
    /// <summary>
    /// Modbus TCP üzerinden sayaç verilerini toplayan, ön hesaplama (Pre-Calculation) yapan
    /// ve verileri periyodik olarak (15 dk/1 saat) özetleyen arka plan servisi.
    /// </summary>
    public class ModbusCollectorWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ModbusCollectorWorker> _logger;

        public ModbusCollectorWorker(IServiceScopeFactory serviceScopeFactory, ILogger<ModbusCollectorWorker> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Modbus Collector Worker (V2 - Smart Timing & Aggregation) Başlatıldı.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // --- 1. AKILLI ZAMANLAMA (CRON MANTIĞI) ---
                    // Sistem rastgele değil, her saatin çeyreğinde (00, 15, 30, 45) çalışacak şekilde kendini ayarlar.
                    // Bu sayede raporlamada "14:15 verisi" dediğimizde tam olarak o zamanı kastetmiş oluruz.
                    var now = DateTime.Now;
                    int minutesToAdd = 15 - (now.Minute % 15);

                    // Saniyeleri ve milisaniyeleri sıfırlayarak tam hedef zamanı oluşturuyoruz.
                    var targetRunTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0)
                        .AddMinutes(minutesToAdd);

                    var delay = targetRunTime - DateTime.Now;
                    if (delay.TotalMilliseconds <= 0) delay = TimeSpan.FromSeconds(1); // Negatif süre koruması

                    _logger.LogInformation($"Bir sonraki okuma döngüsü bekleniyor. Hedef Zaman: {targetRunTime:HH:mm:ss} (Kalan: {delay.TotalMinutes:F1} dk)");

                    // Hedef zamana kadar sistemi uyutuyoruz.
                    await Task.Delay(delay, stoppingToken);

                    // --- 2. VERİ TOPLAMA VE İŞLEME ---
                    // 'targetRunTime' parametresini gönderiyoruz. Böylece işlem 14:16'da bitse bile
                    // veritabanına kayıt saati olarak 14:15:00 işlenecek. Bu raporlamada tutarlılık sağlar.
                    await ProcessAllGatewaysAsync(targetRunTime, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Uygulama kapanıyor, döngüyü kır.
                    break;
                }
                catch (Exception ex)
                {
                    // Beklenmeyen bir hata olursa servisin çökmesini engelle ve 1 dk sonra tekrar dene.
                    _logger.LogCritical(ex, "Döngüsel zamanlama hatası! Servis 1 dakika sonra tekrar deneyecek.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task ProcessAllGatewaysAsync(DateTime recordTime, CancellationToken ct)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 1. Aktif Modbus Gateway Cihazlarını Getir
                // AsNoTracking kullanıyoruz çünkü bu listeyi değiştirmeyeceğiz, sadece okuyacağız. Performans artırır.
                var activeDevices = await dbContext.ModbusDevices
                    .Where(d => d.IsActive)
                    .AsNoTracking()
                    .ToListAsync(ct);

                // Aynı IP adresine sahip cihazları grupluyoruz (Multiplexing).
                // Böylece her IP için sadece bir kez TCP bağlantısı açıp kapatacağız.
                var gatewayGroups = activeDevices.GroupBy(d => d.IpAddress);

                foreach (var gatewayGroup in gatewayGroups)
                {
                    string ipAddress = gatewayGroup.Key;
                    int port = gatewayGroup.First().Port;

                    _logger.LogInformation($"Gateway Bağlanıyor: {ipAddress}:{port}");

                    try
                    {
                        using (TcpClient client = new TcpClient())
                        {
                            // Bağlantı Zaman Aşımı (3 sn)
                            var connectTask = client.ConnectAsync(ipAddress, port);
                            if (await Task.WhenAny(connectTask, Task.Delay(3000, ct)) != connectTask)
                            {
                                throw new TimeoutException($"{ipAddress} adresine bağlanılamadı (Timeout).");
                            }

                            var factory = new ModbusFactory();
                            IModbusMaster master = factory.CreateMaster(client);
                            master.Transport.Retries = 2;     // Hata durumunda 2 kere daha dene
                            master.Transport.ReadTimeout = 2000; // Okuma başına 2 sn bekle

                            // Bu Gateway altındaki tüm UnitID'leri (Slave Cihazları) sırayla oku
                            foreach (var deviceRecord in gatewayGroup)
                            {
                                await ProcessSingleDeviceAsync(dbContext, master, deviceRecord, recordTime, ct);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Bir Gateway hatası diğerlerini etkilememeli, loglayıp devam ediyoruz.
                        _logger.LogError(ex, $"Gateway Hatası ({ipAddress}). Bu IP'deki cihazlar okunamadı.");
                    }
                }
            }
        }

        private async Task ProcessSingleDeviceAsync(
            AppDbContext dbContext,
            IModbusMaster master,
            ModbusDevice deviceRecord,
            DateTime recordTime,
            CancellationToken ct)
        {
            // Bu UnitID'ye bağlı Sayaçları (Meter) ve Register tanımlarını getir
            var meters = await dbContext.Meters
                .Include(m => m.ModbusRegisters).ThenInclude(r => r.MeasurementParameter)
                .Include(m => m.ChildMeters) // Sanal sayaçlar (Virtual Meters) için gerekli
                .Where(m => m.ModbusDeviceId == deviceRecord.Id)
                .ToListAsync(ct);


            // --- FAZ 1: Sadece OKUMA (Memory'e Alma) ---
            // Veritabanı işlemi yok, sadece Modbus hattından hızlıca veri çekiyoruz.
            var capturedData = new List<(Meter Meter, ModbusRegister Reg, double Value)>();

            foreach (var meter in meters)
            {
                foreach (var reg in meter.ModbusRegisters)
                {
                    try
                    {
                        ushort points = (ushort)(reg.DataType == "Float" || reg.DataType == "Int32" ? 2 : 1);
                        ushort[] inputs = await master.ReadHoldingRegistersAsync(deviceRecord.UnitId, (ushort)reg.RegisterAddress, points);

                        // SORU 2 ÇÖZÜMÜ: ModbusHelper kullanılıyor
                        double rawValue = ModbusHelper.ConvertModbusData(inputs, reg.DataType, reg.ByteOrder);
                        double finalValue = rawValue * reg.ScaleFactor;

                        capturedData.Add((meter, reg, finalValue));

                        // Hattı çok boğmamak için mikroskobik bekleme (Opsiyonel)
                        await Task.Delay(10, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Okuma Hatası! Meter: {meter.Name}, Reg: {reg.RegisterAddress}. Hata: {ex.Message}");
                    }
                }
            }

            // --- FAZ 2: KAYIT VE HESAPLAMA (Veritabanı İşlemleri) ---
            // Modbus ile işimiz bitti, artık elimizdeki veriyi işleyip kaydedebiliriz.
            if (capturedData.Any())
            {
                using (var transaction = await dbContext.Database.BeginTransactionAsync(ct))
                {
                    try
                    {
                        foreach (var item in capturedData)
                        {
                            var meter = item.Meter;
                            var reg = item.Reg;
                            var value = item.Value;

                            // 1. Ham Veriyi Ekle
                            var history = new MeterHistory
                            {
                                MeterId = meter.Id,
                                Timestamp = recordTime,
                                Value = value,
                                MeasurementParameterId = reg.MeasurementParameterId
                            };
                            dbContext.MeterHistories.Add(history);

                            // Sanal Sayaçlar (Raw)
                            if (meter.ChildMeters != null)
                            {
                                foreach (var vChild in meter.ChildMeters.Where(c => c.IsVirtual))
                                {
                                    dbContext.MeterHistories.Add(new MeterHistory
                                    {
                                        MeterId = vChild.Id,
                                        Timestamp = recordTime,
                                        Value = value * vChild.VirtualMultiplier,
                                        MeasurementParameterId = reg.MeasurementParameterId
                                    });
                                }
                            }

                            // 2. Tüketim Hesapla (ActiveEnergy ise)
                            if (reg.MeasurementParameter.Key == "ActiveEnergy")
                            {
                                await CalculateAndSaveConsumption(dbContext, meter, reg, value, recordTime, ct);

                                // Sanal Sayaç Tüketimi
                                if (meter.ChildMeters != null)
                                {
                                    foreach (var vChild in meter.ChildMeters.Where(c => c.IsVirtual))
                                    {
                                        double virtualVal = value * vChild.VirtualMultiplier;
                                        await CalculateAndSaveConsumption(dbContext, vChild, reg, virtualVal, recordTime, ct);
                                    }
                                }
                            }
                        }

                        await dbContext.SaveChangesAsync(ct);
                        await transaction.CommitAsync(ct);
                        _logger.LogInformation($"Cihaz İşlendi: UnitId {deviceRecord.UnitId} ({capturedData.Count} register)");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync(ct);
                        _logger.LogError($"Veritabanı Kayıt Hatası (Unit {deviceRecord.UnitId}): {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Geçmiş verilerle kıyaslayıp 15 dakikalık ve (gerekirse) saatlik tüketimi hesaplar ve kaydeder.
        /// </summary>
        private async Task CalculateAndSaveConsumption(
            AppDbContext dbContext,
            Meter meter,
            ModbusRegister reg,
            double currentValue,
            DateTime currentTimestamp,
            CancellationToken ct)
        {
            // 1. Önceki Geçerli Kaydı Bul
            // Bu sayacın, şu anki zamandan önceki en son okumasını getiriyoruz.
            var lastHistory = await dbContext.MeterHistories
                .Where(h => h.MeterId == meter.Id &&
                            h.MeasurementParameterId == reg.MeasurementParameterId &&
                            h.Timestamp < currentTimestamp)
                .OrderByDescending(h => h.Timestamp)
                .FirstOrDefaultAsync(ct);

            decimal consumption = 0;

            if (lastHistory != null)
            {
                decimal currentValDec = (decimal)currentValue;
                decimal lastValDec = (decimal)lastHistory.Value;

                // Tüketim = Şimdiki - Önceki
                if (currentValDec >= lastValDec)
                {
                    consumption = currentValDec - lastValDec;
                }
                else
                {
                    // Rollover Durumu (9999 -> 0005)
                    // Sayaç başa sardıysa veya değiştiyse, şimdiki değeri tüketim kabul ediyoruz.
                    consumption = currentValDec;
                    _logger.LogWarning($"Sayaç Rollover Tespit Edildi: {meter.Name}. Eski: {lastValDec}, Yeni: {currentValDec}");
                }
            }

            // 2. 15 Dakikalık Özeti Kaydet ("15Min")
            // İlk okumada (lastHistory null ise) consumption 0 olur, bu beklenen davranıştır.
            if (consumption >= 0)
            {
                var consumptionEntry = new MeterConsumption
                {
                    MeterId = meter.Id,
                    Timestamp = currentTimestamp,
                    Consumption = consumption,
                    PeriodType = "15Min" // Sabit
                };
                dbContext.MeterConsumptions.Add(consumptionEntry);
            }

            // 3. Saatlik Özet Hesaplama (Aggregation) ("Hourly")
            // Eğer zaman tam saat başıysa (Örn: 14:00, 15:00)
            if (currentTimestamp.Minute == 0)
            {
                var rangeStart = currentTimestamp.AddHours(-1); // 14:00
                var rangeEnd = currentTimestamp;                // 15:00

                // Son 1 saatin 15 dakikalık verilerini veritabanından değil, 
                // henüz kaydedilmemiş (memory'deki) veriyi de dahil ederek hesaplamalıyız.
                // Güvenli yöntem: Önceki 3 çeyreği DB'den çek + Şimdiki (consumption) topla.

                var previousSum = await dbContext.MeterConsumptions
                    .Where(mc => mc.MeterId == meter.Id &&
                                 mc.PeriodType == "15Min" &&
                                 mc.Timestamp > rangeStart &&
                                 mc.Timestamp < rangeEnd) // Şimdiki zamanı (rangeEnd) dahil etme, o zaten elimizde (consumption)
                    .SumAsync(mc => mc.Consumption, ct);

                decimal totalHourly = previousSum + consumption;

                // Saatlik kaydı ekle
                dbContext.MeterConsumptions.Add(new MeterConsumption
                {
                    MeterId = meter.Id,
                    Timestamp = currentTimestamp,
                    Consumption = totalHourly,
                    PeriodType = "Hourly"
                });

                _logger.LogInformation($"Saatlik Hesaplama Tamamlandı ({meter.Name}): {totalHourly} kWh");
            }
        }

    }
}