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

        // 4. Karbon Emisyonu Hesaplama
        [HttpGet("emission")]
        public async Task<IActionResult> GetEmission(int meterId, DateTime start, DateTime end)
        {
            double co2 = await _emissionService.CalculateTotalEmission(meterId, start, end);
            return Ok(new { MeterId = meterId, Emission = co2, Unit = "kgCO2" });
        }
    }
}
