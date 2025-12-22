using EnergyMonitoringSystem.Core.Entities;
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
            // 1. Aktif Enerji Parametresini Bul
            var activeEnergyParam = await _context.MeasurementParameters
                .FirstOrDefaultAsync(p => p.Key == "ActiveEnergy");

            if (activeEnergyParam == null) return 0;

            // TÜKETİM HESABI (DELTA: SON - İLK)
            var historyQuery = _context.MeterHistories
                .Where(h => h.MeterId == meterId
                            && h.MeasurementParameterId == activeEnergyParam.Id
                            && h.Timestamp >= startDate
                            && h.Timestamp <= endDate);

            var firstRecord = await historyQuery.OrderBy(h => h.Timestamp).FirstOrDefaultAsync();
            var lastRecord = await historyQuery.OrderByDescending(h => h.Timestamp).FirstOrDefaultAsync();

            double totalKwh = 0;

            if (firstRecord != null && lastRecord != null)
            {
                if (lastRecord.Value >= firstRecord.Value)
                {
                    totalKwh = lastRecord.Value - firstRecord.Value;
                }
                else
                {
                    // Sayaç sıfırlanması durumu
                    totalKwh = lastRecord.Value;
                }
            }

            // 2. Tüketimi Topla
            //var totalKwh = await _context.MeterHistories
            //    .Where(h => h.MeterId == meterId
            //                && h.MeasurementParameterId == activeEnergyParam.Id // <--- DEĞİŞİKLİK BURADA
            //                && h.Timestamp >= startDate
            //                && h.Timestamp <= endDate)
            //    .SumAsync(h => h.Value);

            if (totalKwh == 0) return 0;

            // 3. Karbon Faktörünü Bul (Aynen Kalıyor)
            var activeFactor = await _context.CarbonFactors
                .Where(f => f.ValidFrom <= endDate)
                .OrderByDescending(f => f.ValidFrom)
                .FirstOrDefaultAsync();

            double factorValue = activeFactor?.Factor ?? 0.44;

            return totalKwh * factorValue;
        }
    }
}