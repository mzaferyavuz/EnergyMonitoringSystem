using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Core.Enums; // RegisterType enum'ı için
using EnergyMonitoringSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitoringSystem.Service.Emission
{
    public class EmissionService
    {
        private readonly AppDbContext _context;

        public EmissionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<double> CalculateTotalEmission(int meterId, DateTime startDate, DateTime endDate)
        {
            // 1. İlgili tarih aralığındaki toplam kWh tüketimini bul
            var totalKwh = await _context.MeterHistories
                .Where(h => h.MeterId == meterId
                            && h.Type == RegisterType.kWh
                            && h.Timestamp >= startDate
                            && h.Timestamp <= endDate)
                .SumAsync(h => h.Value);

            if (totalKwh == 0) return 0;

            // 2. O tarih için geçerli olan en güncel Karbon Faktörünü bul
            // (Örn: 2024 yılı için farklı, 2025 için farklı faktör olabilir)
            var activeFactor = await _context.CarbonFactors
                .Where(f => f.ValidFrom <= endDate)
                .OrderByDescending(f => f.ValidFrom)
                .FirstOrDefaultAsync();

            // Eğer faktör tanımlanmamışsa varsayılan bir değer kullan (Örn: TR şebekesi ortalaması ~0.44 kg/kWh)
            double factorValue = activeFactor?.Factor ?? 0.44;

            // 3. Sonuç: Tüketim * Faktör = Toplam kgCO2
            return totalKwh * factorValue;
        }
    }
}