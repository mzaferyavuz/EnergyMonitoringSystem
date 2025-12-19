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

            // 2. Sadece Aktif Enerji verilerini topla
            var totalConsumption = (decimal)await _context.MeterHistories
                .Where(h => h.MeterId == meterId
                            && h.MeasurementParameterId == activeEnergyParam.Id // <--- DEĞİŞİKLİK BURADA
                            && h.Timestamp >= startDate
                            && h.Timestamp <= endDate)
                .SumAsync(h => h.Value);

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