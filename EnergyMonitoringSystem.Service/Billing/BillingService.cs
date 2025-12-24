using EnergyMonitoringSystem.Core.DTOs;
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

        public async Task<TenantBillDto> CalculateTenantBill(int tenantId, DateTime startDate, DateTime endDate)
        {
            // 1. Kiracıyı, Sayaçlarını, Sayaçların Amaçlarını ve PARENT bilgilerini getir
            var tenant = await _context.Tenants
                .Include(t => t.Meters)
                    .ThenInclude(m => m.Purpose)      // Tablo sütunu için Purpose (UsagePurpose)
                .Include(t => t.Meters)
                    .ThenInclude(m => m.ParentMeter)  // Hiyerarşi kontrolü için Parent
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null) throw new Exception("Kiracı bulunamadı.");

            // DTO Başlangıç
            var billReport = new TenantBillDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                TaxNumber = tenant.TaxNumber,
                FilterStartDate = startDate,
                FilterEndDate = endDate
            };

            // 2. Tarife Belirleme
            var tariff = await _context.Tariffs
                .Where(t => t.TenantId == tenantId && t.ValidFrom <= endDate)
                .OrderByDescending(t => t.ValidFrom)
                .FirstOrDefaultAsync();

            // Özel tarife yoksa genel tarifeyi al
            if (tariff == null)
            {
                tariff = await _context.Tariffs
                   .Where(t => t.TenantId == null && t.ValidFrom <= endDate)
                   .OrderByDescending(t => t.ValidFrom)
                   .FirstOrDefaultAsync();
            }

            billReport.UnitPrice = tariff != null ? (decimal)tariff.UnitPrice : 0;

            // 3. Enerji Parametresi (ActiveEnergy)
            var activeEnergyParam = await _context.MeasurementParameters
                .FirstOrDefaultAsync(p => p.Key == "ActiveEnergy");

            if (activeEnergyParam == null) return billReport; // Parametre yoksa boş dön

            // 4. Sayaç Döngüsü
            foreach (var meter in tenant.Meters)
            {
                // --- A. Veri Çekme ---
                var historyQuery = _context.MeterHistories
                    .Where(h => h.MeterId == meter.Id
                                && h.MeasurementParameterId == activeEnergyParam.Id
                                && h.Timestamp >= startDate
                                && h.Timestamp <= endDate);

                var firstRecord = await historyQuery.OrderBy(h => h.Timestamp).FirstOrDefaultAsync();
                var lastRecord = await historyQuery.OrderByDescending(h => h.Timestamp).FirstOrDefaultAsync();

                // --- B. Detay Satırı Oluşturma ---
                var detail = new MeterBillDetailDto
                {
                    MeterId = meter.Id,
                    MeterName = meter.Name,
                    UsagePurpose = meter.Purpose?.Name, // Purpose tablosundan gelen isim

                    // Eğer veri varsa değerini, yoksa null ata (Tabloda "-" göstermek için)
                    FirstIndex = firstRecord != null ? (decimal)firstRecord.Value : null,
                    FirstIndexDate = firstRecord?.Timestamp,

                    LastIndex = lastRecord != null ? (decimal)lastRecord.Value : null,
                    LastIndexDate = lastRecord?.Timestamp
                };

                // --- C. Tüketim Hesapla ---
                if (detail.FirstIndex.HasValue && detail.LastIndex.HasValue)
                {
                    if (detail.LastIndex >= detail.FirstIndex)
                        detail.Consumption = detail.LastIndex.Value - detail.FirstIndex.Value;
                    else
                        detail.Consumption = detail.LastIndex.Value; // Sıfırlanma durumu
                }
                else
                {
                    detail.Consumption = 0; // Veri yoksa tüketim 0
                }

                // --- D. Satır Tutarı ---
                detail.Amount = detail.Consumption * billReport.UnitPrice;


                // --- E. HİYERARŞİ VE TOPLAM KONTROLÜ (KRİTİK KISIM) ---
                // Kural: Eğer bu sayacın bir Parent'ı varsa VE o Parent da AYNI KİRACIYA aitse, 
                // bu sayaç "Alt Sayaç"tır ve toplama dahil edilmez.
                bool isSubMeterOfSameTenant = meter.ParentMeterId.HasValue &&
                                              meter.ParentMeter != null &&
                                              meter.ParentMeter.TenantId == tenantId;

                if (isSubMeterOfSameTenant)
                {
                    detail.IsExcludedFromTotal = true;
                    detail.Note = "Bağlı olduğu ana sayaç faturaya dahil olduğu için toplama eklenmedi.";
                }
                else
                {
                    // Ana sayaçtır veya Parent'ı başka birine aittir -> Topla!
                    detail.IsExcludedFromTotal = false;
                    billReport.TotalConsumption += detail.Consumption;
                    billReport.TotalAmount += detail.Amount;
                }

                billReport.MeterDetails.Add(detail);
            }

            return billReport;
        }
    }
}