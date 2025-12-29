using EnergyMonitoringSystem.Data;
using EnergyMonitoringSystem.Service.Billing;
using EnergyMonitoringSystem.Service.Emission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly BillingService _billingService;
        private readonly EmissionService _emissionService;

        public ReportController(AppDbContext context, BillingService billingService, EmissionService emissionService)
        {
            _context = context;
            _billingService = billingService;
            _emissionService = emissionService;
        }

        // 1. Grafik Verisi: Belirli bir sayacın, belirli parametreye (örn: Voltaj) ait geçmiş verisi
        [HttpGet("history")]
        public async Task<IActionResult> GetMeterHistory(int meterId, int parameterId, DateTime start, DateTime end)
        {
            var history = await _context.MeterHistories
                .Where(h => h.MeterId == meterId &&
                            h.MeasurementParameterId == parameterId &&
                            h.Timestamp >= start &&
                            h.Timestamp <= end)
                .OrderBy(h => h.Timestamp)
                .Select(h => new { h.Timestamp, h.Value })
                .ToListAsync();

            return Ok(history);
        }

        // 2. Dashboard Widget: Bir sayacın son okunan değeri
        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestValue(int meterId, int parameterId)
        {
            var latest = await _context.MeterHistories
                .Where(h => h.MeterId == meterId && h.MeasurementParameterId == parameterId)
                .OrderByDescending(h => h.Timestamp)
                .FirstOrDefaultAsync();

            if (latest == null) return NotFound("Veri yok");
            return Ok(new { latest.Timestamp, latest.Value });
        }

        [HttpGet("latest-all-parameters/{meterId}")]
        public async Task<IActionResult> GetLatestAllParameters(int meterId)
        {
            // 1. Önce Sayacın varlığını kontrol et (Production önlemi)
            bool meterExists = await _context.Meters.AnyAsync(m => m.Id == meterId);
            if (!meterExists)
            {
                return NotFound("Belirtilen ID ile bir sayaç bulunamadı.");
            }
            // Sayaca ait tüm parametrelerin en son kayıtlarını getir
            var latestValues = await _context.ModbusRegisters
                    .Where(r => r.MeterId == meterId)
                    .Select(r => new
                    {
                        ParameterName = r.MeasurementParameter.Name,
                        Unit = r.MeasurementParameter.Unit,
                        // Alt sorgu (Subquery) ile en son değeri çekiyoruz. EF Core bunu çok iyi optimize eder.
                        LatestData = _context.MeterHistories
                            .Where(h => h.MeterId == meterId && h.MeasurementParameterId == r.MeasurementParameterId)
                            .OrderByDescending(h => h.Timestamp)
                            .Select(h => new { h.Value, h.Timestamp })
                            .FirstOrDefault()
                    })
                    .ToListAsync();

            var result = latestValues.Select(x => new
            {
                x.ParameterName,
                x.Unit,
                Value = x.LatestData != null ? x.LatestData.Value : 0,
                LastRead = x.LatestData != null ? x.LatestData.Timestamp : (DateTime?)null
            });

            return Ok(latestValues);
        }

        // 3. Fatura Hesaplama
        [HttpGet("bill")]
        public async Task<IActionResult> GetBill(int meterId, DateTime start, DateTime end)
        {
            try
            {
                decimal amount = await _billingService.CalculateBill(meterId, start, end);
                return Ok(new { MeterId = meterId, Amount = amount, Currency = "TL", StartDate = start, EndDate = end });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Belirtilen kiracı için detaylı fatura raporu oluşturur.
        /// Ana sayaç/Alt sayaç mantığını ve tarih aralığını dikkate alır.
        /// </summary>
        /// <param name="tenantId">Kiracı ID</param>
        /// <param name="startDate">Başlangıç Tarihi (Örn: 2025-12-01)</param>
        /// <param name="endDate">Bitiş Tarihi (Örn: 2025-12-31)</param>
        /// <returns>TenantBillDto</returns>
        [HttpGet("tenant-bill-report")]
        public async Task<IActionResult> GetBillReport(
            [FromQuery] int tenantId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            // 1. Basit Validasyonlar
            if (tenantId <= 0)
            {
                return BadRequest("Geçerli bir Kiracı ID (TenantId) girilmelidir.");
            }

            if (startDate > endDate)
            {
                return BadRequest("Başlangıç tarihi bitiş tarihinden büyük olamaz.");
            }

            try
            {
                // 2. Servise Git ve Hesaplanmış Raporu Al
                var billReport = await _billingService.CalculateTenantBill(tenantId, startDate, endDate);

                // 3. Sonucu Dön (JSON formatında)
                return Ok(billReport);
            }
            catch (Exception ex)
            {
                // Servis içinde "Kiracı bulunamadı" veya "Parametre yok" gibi hatalar fırlatılırsa burada yakalıyoruz.
                return BadRequest(new { ErrorMessage = ex.Message });
            }
        }


        // 4. Karbon Emisyonu Hesaplama
        [HttpGet("emission")]
        public async Task<IActionResult> GetEmission(int meterId, DateTime start, DateTime end)
        {
            double co2 = await _emissionService.CalculateTotalEmission(meterId, start, end);
            return Ok(new { MeterId = meterId, Emission = co2, Unit = "kgCO2" });
        }
    }
}
