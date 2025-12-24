using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Core.Enums;
using EnergyMonitoringSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitoringSystem.Service.Services
{
    public class DashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardResponseDto> GetDashboardData(DashboardRequestDto request)
        {
            var response = new DashboardResponseDto();

            // 1. SAYAÇLARI BELİRLE (Scope ve Hiyerarşi Mantığı)
            var meterQuery = _context.Meters
                .Include(m => m.Purpose)
                .Include(m => m.ParentMeter) // Hiyerarşi kontrolü için
                .AsQueryable();

            List<Meter> targetMeters = new List<Meter>();

            // Tüm sayaçları çekip memory'de filtrelemek karmaşık hiyerarşi için bazen daha güvenlidir
            // Ancak performans için DB seviyesinde filtrelemeye çalışalım:
            var allMeters = await meterQuery.ToListAsync();

            switch (request.Scope)
            {
                case DashboardScope.All:
                    // Herhangi bir parent'ı olmayanlar (En tepe kökler)
                    targetMeters = allMeters.Where(m => m.ParentMeterId == null).ToList();
                    break;

                case DashboardScope.NoTenant:
                    // Tenant'ı olmayan VE Parent'ı olmayanlar
                    targetMeters = allMeters.Where(m => m.TenantId == null && m.ParentMeterId == null).ToList();
                    break;

                case DashboardScope.Tenant:
                    if (request.TenantIds == null || !request.TenantIds.Any())
                        throw new Exception("Tenant seçimi yapılmadı.");

                    // Kural: Tenant seçildiyse, parent'ı başkasına ait olsa bile o tenant için en üst sayaçsa al.
                    targetMeters = allMeters.Where(m =>
                        m.TenantId.HasValue &&
                        request.TenantIds.Contains(m.TenantId.Value) &&
                        (m.ParentMeterId == null || // Parent yoksa zaten en üsttür
                         (m.ParentMeter != null && !request.TenantIds.Contains(m.ParentMeter.TenantId ?? 0))) // Parent var ama bu Tenant'a ait değilse
                    ).ToList();
                    break;
            }

            var meterIds = targetMeters.Select(m => m.Id).ToList();

            // 2. REFERANS VERİLERİ (Tarife ve Emisyon)
            // Performans için aralıktaki tüm ilgili tarifeleri ve karbon faktörlerini çekiyoruz
            var relevantTariffs = await _context.Tariffs
                .Where(t => t.ValidFrom <= request.EndDate)
                .ToListAsync();

            var carbonFactors = await _context.CarbonFactors
                .Where(c => c.ValidFrom <= request.EndDate)
                .OrderByDescending(c => c.ValidFrom)
                .ToListAsync();

            // 3. ZAMAN DİLİMLERİNİ OLUŞTUR (Interval'e göre)
            var timeBuckets = CreateTimeBuckets(request.StartDate, request.EndDate, request.Interval);
            response.Labels = timeBuckets.Select(t => t.Label).ToList();

            // 4. VERİLERİ ÇEK VE GRUPLA (UsagePurpose Bazlı)
            // DB'den sadece ilgili tarih ve sayaçların verisini çekiyoruz
            var historyData = await _context.MeterHistories
                .Where(h => meterIds.Contains(h.MeterId) && h.Timestamp >= request.StartDate && h.Timestamp <= request.EndDate)
                .Select(h => new { h.MeterId, h.Value, h.Timestamp }) // Sadece gereken alanlar
                .ToListAsync();

            // Sayaçları Kullanım Amacına Göre Grupla
            var metersByPurpose = targetMeters
                .GroupBy(m => m.Purpose?.Name ?? "Diğer") // Purpose yoksa "Diğer"
                .ToList();

            foreach (var purposeGroup in metersByPurpose)
            {
                var series = new DashboardSeriesDto { PurposeName = purposeGroup.Key };

                // Her zaman dilimi için hesaplama
                foreach (var bucket in timeBuckets)
                {
                    decimal bucketConsumption = 0;
                    decimal bucketCost = 0;
                    decimal bucketEmission = 0;

                    // Bu bucket için geçerli Global Emisyon Faktörü
                    var activeCarbon = carbonFactors.FirstOrDefault(c => c.ValidFrom <= bucket.Start)?.Factor ?? 0;

                    // Bu gruptaki her sayaç için o aralıktaki değişimi bul
                    foreach (var meter in purposeGroup)
                    {
                        // Memory'deki history listesinden o aralığa düşenleri bul
                        var meterLogs = historyData
                            .Where(h => h.MeterId == meter.Id && h.Timestamp >= bucket.Start && h.Timestamp < bucket.End)
                            .OrderBy(h => h.Timestamp)
                            .ToList();

                        if (meterLogs.Count > 1)
                        {
                            var firstVal = meterLogs.First().Value;
                            var lastVal = meterLogs.Last().Value;

                            // Tüketim (Rollover kontrolü basitçe eklendi)
                            decimal consumption = (decimal)(lastVal >= firstVal ? lastVal - firstVal : lastVal);

                            if (consumption > 0)
                            {
                                bucketConsumption += consumption;

                                // --- MALİYET HESABI ---
                                // Sayacın Tarifesini Bul (Tenant'a özel veya Genel)
                                var activeTariff = relevantTariffs
                                    .Where(t => (t.TenantId == meter.TenantId || (meter.TenantId == null && t.TenantId == null))
                                                && t.ValidFrom <= bucket.Start)
                                    .OrderByDescending(t => t.TenantId) // Önce Tenant'a özel, sonra genel
                                    .ThenByDescending(t => t.ValidFrom) // En güncel tarihli
                                    .FirstOrDefault();

                                decimal price = activeTariff?.UnitPrice ?? 0;
                                bucketCost += consumption * price;

                                // --- EMİSYON HESABI ---
                                bucketEmission += consumption * (decimal)activeCarbon;
                            }
                        }
                    }

                    // Series'e ekle
                    series.ConsumptionData.Add(bucketConsumption);
                    series.CostData.Add(bucketCost);
                    series.EmissionData.Add(bucketEmission);

                    // Genel Toplamlara Ekle
                    response.TotalConsumption += bucketConsumption;
                    response.TotalCost += bucketCost;
                    response.TotalEmission += bucketEmission;
                }

                response.Series.Add(series);
            }

            return response;
        }

        // Zaman dilimlerini oluşturan yardımcı metot
        private List<(DateTime Start, DateTime End, string Label)> CreateTimeBuckets(DateTime start, DateTime end, DashboardInterval interval)
        {
            var buckets = new List<(DateTime, DateTime, string)>();
            var current = start;

            while (current < end)
            {
                DateTime next;
                string label;

                switch (interval)
                {
                    case DashboardInterval.FifteenMinutes:
                        next = current.AddMinutes(15);
                        label = current.ToString("dd.MM HH:mm");
                        break;
                    case DashboardInterval.Hourly:
                        next = current.AddHours(1);
                        label = current.ToString("dd.MM HH:mm");
                        break;
                    case DashboardInterval.Daily:
                        next = current.AddDays(1);
                        label = current.ToString("dd.MM.yyyy");
                        break;
                    case DashboardInterval.Weekly:
                        next = current.AddDays(7);
                        label = $"{current:dd.MM}-{current.AddDays(6):dd.MM}";
                        break;
                    case DashboardInterval.Monthly:
                        next = current.AddMonths(1);
                        label = current.ToString("MMMM yyyy");
                        break;
                    case DashboardInterval.Yearly:
                        next = current.AddYears(1);
                        label = current.ToString("yyyy");
                        break;
                    default:
                        next = current.AddDays(1);
                        label = current.ToString("dd.MM");
                        break;
                }

                // Eğer son parça bitiş tarihini aşıyorsa, bitiş tarihine kadar al
                if (next > end) next = end;

                buckets.Add((current, next, label));
                current = next;
            }

            return buckets;
        }
    }
}