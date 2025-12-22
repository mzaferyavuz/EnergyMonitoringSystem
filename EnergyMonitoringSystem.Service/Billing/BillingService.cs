using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitoringSystem.Service.Billing
{
    public class BillingService
    {
        private readonly AppDbContext _context;

        public BillingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> CalculateBill(int meterId, DateTime startDate, DateTime endDate)
        {
            // 1. Sayacın kime ait olduğunu bul (TenantId var mı?)
            var meter = await _context.Meters.FindAsync(meterId);
            if (meter == null) return 0;

            // 1. "Aktif Enerji" parametresinin ID'sini bul (Key = "ActiveEnergy")
            // (Performans için bu ID cache'lenebilir ama şimdilik veritabanından soralım)
            var activeEnergyParam = await _context.MeasurementParameters
                .FirstOrDefaultAsync(p => p.Key == "ActiveEnergy");

            if (activeEnergyParam == null) return 0; // Sistemde enerji parametresi tanımlı değilse hesap yapamayız

            // İlgili tarih aralığındaki kayıtları tarih sırasına göre sorgula
            var historyQuery = _context.MeterHistories
                .Where(h => h.MeterId == meterId
                            && h.MeasurementParameterId == activeEnergyParam.Id
                            && h.Timestamp >= startDate
                            && h.Timestamp <= endDate);

            // İlk ve Son okumayı bul
            // Not: FirstOrDefaultAsync kullanmak için önce OrderBy yapmalıyız.
            // Performans notu: Veri çoksa bu sorguyu ikiye bölmek (Min ve Max Timestamp çekmek) daha hızlı olabilir.
            var firstRecord = await historyQuery.OrderBy(h => h.Timestamp).FirstOrDefaultAsync();
            var lastRecord = await historyQuery.OrderByDescending(h => h.Timestamp).FirstOrDefaultAsync();

            decimal totalConsumption = 0;

            if (firstRecord != null && lastRecord != null)
            {
                // Sayaç sıfırlanmadıysa (Rollover durumu yoksa) Son - İlk
                if (lastRecord.Value >= firstRecord.Value)
                {
                    totalConsumption = (decimal)(lastRecord.Value - firstRecord.Value);
                }
                else
                {
                    // DİKKAT: Sayaç başa sarmış olabilir (Örn: 9999 -> 0005)
                    // Basit bir yaklaşım olarak, eğer son değer ilk değerden küçükse;
                    // bu aralıkta sayaç değişmiş veya sıfırlanmış demektir.
                    // Şimdilik sadece son okunanı alabiliriz veya hata loglayabiliriz.
                    // Profesyonel çözümde ardışık farkların toplamı (sum of deltas) alınır.
                    // Basitlik adına şimdilik farkı alıyoruz (Negatif çıkmaması için kontrol):
                    totalConsumption = (decimal)lastRecord.Value; // (Sıfırlandıysa o anki değer kadar tüketmiştir varsayımı)
                }
            }


            // 2. Sadece Aktif Enerji verilerini topla
            //var totalConsumption = (decimal)await _context.MeterHistories
            //    .Where(h => h.MeterId == meterId
            //                && h.MeasurementParameterId == activeEnergyParam.Id // <--- DEĞİŞİKLİK BURADA
            //                && h.Timestamp >= startDate
            //               && h.Timestamp <= endDate)
            //    .SumAsync(h => h.Value);

            if (totalConsumption == 0) return 0;

            // 3. Tarife Bulma (Aynen Kalıyor)
            var activeTariff = await _context.Tariffs
                .Where(t => (t.TenantId == meter.TenantId || t.TenantId == null) && t.ValidFrom <= endDate)
                .OrderByDescending(t => t.TenantId)
                .ThenByDescending(t => t.ValidFrom)
                .FirstOrDefaultAsync();

            decimal unitPrice = activeTariff?.UnitPrice ?? 0;

            return totalConsumption * unitPrice;
        }
    }
}