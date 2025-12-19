using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Core.Enums;
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

            // 2. Toplam Tüketimi (kWh) hesapla
            // Not: MeterHistory value 'double' olduğu için 'decimal'e cast ediyoruz (para hesabı hassas olmalı)
            var totalConsumption = (decimal)await _context.MeterHistories
                .Where(h => h.MeterId == meterId
                            && h.Type == RegisterType.kWh
                            && h.Timestamp >= startDate
                            && h.Timestamp <= endDate)
                .SumAsync(h => h.Value);

            if (totalConsumption == 0) return 0;

            // 3. Geçerli Tarifeyi Bulma Mantığı
            // Öncelik 1: Bu kiracıya özel tanımlanmış tarife var mı?
            // Öncelik 2: Genel (TenantId = null) tarife var mı?
            var activeTariff = await _context.Tariffs
                .Where(t => (t.TenantId == meter.TenantId || t.TenantId == null) && t.ValidFrom <= endDate)
                .OrderByDescending(t => t.TenantId) // Önce Tenant'a özel olanı dene
                .ThenByDescending(t => t.ValidFrom) // Sonra tarihe göre en güncelini al
                .FirstOrDefaultAsync();

            decimal unitPrice = activeTariff?.UnitPrice ?? 0;

            // 4. Sonuç: Tüketim x Birim Fiyat
            return totalConsumption * unitPrice;
        }
    }
}