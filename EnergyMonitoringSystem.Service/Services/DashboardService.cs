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

            try
            {
                // 1. SAYAÇLARI BELİRLE (Kapsam ve Hiyerarşi)
                var allMeters = await _context.Meters
                    .Include(m => m.Purpose)
                    .Include(m => m.ParentMeter)
                    .AsNoTracking() // Sadece okuma yapıyoruz, performans artırır
                    .ToListAsync();

                List<int> targetMeterIds = new List<int>();

                switch (request.Scope)
                {
                    case DashboardScope.All:
                        // Parent'ı olmayan "Kök" sayaçlar (Ana panolar)
                        targetMeterIds = allMeters.Where(m => m.ParentMeterId == null).Select(m => m.Id).ToList();
                        break;

                    case DashboardScope.NoTenant:
                        // Tenant'ı olmayan ve Kök olanlar
                        targetMeterIds = allMeters.Where(m => m.TenantId == null && m.ParentMeterId == null).Select(m => m.Id).ToList();
                        break;

                    case DashboardScope.Tenant:
                        if (request.TenantIds == null || !request.TenantIds.Any())
                            throw new ArgumentException("Tenant seçimi yapılmadı.");

                        // Seçilen Tenant'a ait olup, üstünde aynı tenant'a ait başka sayaç olmayanlar
                        targetMeterIds = allMeters.Where(m =>
                            m.TenantId.HasValue &&
                            request.TenantIds.Contains(m.TenantId.Value) &&
                            (m.ParentMeterId == null || // Kök sayaç
                             (m.ParentMeter != null && !request.TenantIds.Contains(m.ParentMeter.TenantId ?? 0))) // Parent başka tenant'ınsa bu benim kökümdür
                        ).Select(m => m.Id).ToList();
                        break;
                }

                if (!targetMeterIds.Any()) return response; // Sayaç yoksa boş dön

                // 2. ZAMAN DİLİMLERİNİ OLUŞTUR (X Ekseni)
                var timeBuckets = CreateTimeBuckets(request.StartDate, request.EndDate, request.Interval);
                response.Labels = timeBuckets.Select(t => t.Label).ToList();

                // 3. REFERANS VERİLERİ (Performans için önbelleğe al)
                var tariffs = await _context.Tariffs.AsNoTracking().ToListAsync();
                var carbonFactors = await _context.CarbonFactors.AsNoTracking().ToListAsync();

                // 4. VERİLERİ ÇEK VE GRUPLA (Consumption Tablosundan)
                // Hangi çözünürlüğü kullanacağımıza karar veriyoruz (Performans optimizasyonu)
                // Eğer interval 15dk ise "15Min" verisini, saatlik veya üstü ise "Hourly" verisini kullanmak daha hızlıdır.
                string resolution = request.Interval == DashboardInterval.FifteenMinutes ? "15Min" : "Hourly";

                // Veritabanından sadece ilgili aralık ve sayaçların özet verisini çekiyoruz
                var consumptionData = await _context.MeterConsumptions
                    .Where(c => targetMeterIds.Contains(c.MeterId) &&
                                c.PeriodType == resolution &&
                                c.Timestamp >= request.StartDate &&
                                c.Timestamp <= request.EndDate)
                    .Select(c => new { c.MeterId, c.Timestamp, c.Consumption })
                    .AsNoTracking()
                    .ToListAsync();

                // Sayaçları Kullanım Amacına (UsagePurpose) göre grupla
                var metersByPurpose = allMeters
                    .Where(m => targetMeterIds.Contains(m.Id))
                    .GroupBy(m => m.Purpose?.Name ?? "Diğer")
                    .ToList();

                // 5. HESAPLAMA DÖNGÜSÜ
                foreach (var purposeGroup in metersByPurpose)
                {
                    var series = new DashboardSeriesDto { PurposeName = purposeGroup.Key };
                    var purposeMeterIds = purposeGroup.Select(m => m.Id).ToList();

                    foreach (var bucket in timeBuckets)
                    {
                        decimal bucketConsumption = 0;
                        decimal bucketCost = 0;
                        decimal bucketEmission = 0;

                        // Bu zaman diliminin ortasındaki geçerli faktörleri bul
                        var activeCarbon = carbonFactors
                            .Where(c => c.ValidFrom <= bucket.End)
                            .OrderByDescending(c => c.ValidFrom)
                            .FirstOrDefault()?.Factor ?? 0.44; // Varsayılan faktör

                        // Bu gruptaki sayaçların bu bucket içindeki verilerini topla
                        // NOT: Veriler zaten hesaplı (Delta), sadece SUM yapıyoruz.
                        var validConsumptions = consumptionData
                            .Where(c => purposeMeterIds.Contains(c.MeterId) &&
                                        c.Timestamp > bucket.Start &&  // Başlangıç hariç
                                        c.Timestamp <= bucket.End)     // Bitiş dahil
                            .ToList();

                        if (validConsumptions.Any())
                        {
                            bucketConsumption = validConsumptions.Sum(c => c.Consumption);

                            // Maliyet Hesabı (Sayaç bazlı tarife değişebileceği için döngüdeyiz)
                            foreach (var item in validConsumptions)
                            {
                                // İlgili sayacı bul (Tarife tenantId kontrolü için)
                                var meter = purposeGroup.FirstOrDefault(m => m.Id == item.MeterId);
                                if (meter != null)
                                {
                                    var tariff = tariffs
                                        .Where(t => (t.TenantId == meter.TenantId || t.TenantId == null) &&
                                                    t.ValidFrom <= bucket.End)
                                        .OrderByDescending(t => t.TenantId) // Önce Tenant'a özel
                                        .ThenByDescending(t => t.ValidFrom)
                                        .FirstOrDefault();

                                    decimal price = tariff?.UnitPrice ?? 0;
                                    bucketCost += item.Consumption * price;
                                }
                            }

                            // Emisyon Hesabı (Consumption * Global Factor)
                            bucketEmission = bucketConsumption * (decimal)activeCarbon;
                        }

                        // Seri Verilerine Ekle
                        series.ConsumptionData.Add(Math.Round(bucketConsumption, 2));
                        series.CostData.Add(Math.Round(bucketCost, 2));
                        series.EmissionData.Add(Math.Round(bucketEmission, 2));

                        // Genel Toplamlara Ekle
                        response.TotalConsumption += bucketConsumption;
                        response.TotalCost += bucketCost;
                        response.TotalEmission += bucketEmission;
                    }

                    response.Series.Add(series);
                }
            }
            catch (Exception ex)
            {
                // Hata fırlatarak Controller'ın yakalamasını sağlıyoruz veya logluyoruz
                throw new Exception($"Dashboard verisi oluşturulurken hata: {ex.Message}", ex);
            }

            return response;
        }

        // Zaman dilimlerini oluşturan yardımcı metot (Aynı kalabilir, sadece ufak kontroller)
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
                        label = current.ToString("MMM yyyy");
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

                if (next > end) next = end; // Bitiş tarihini aşma

                // Boş aralık oluşmaması için kontrol
                if (current < next)
                {
                    buckets.Add((current, next, label));
                }
                current = next;
            }
            return buckets;
        }
    }
}