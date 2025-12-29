using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Core.Enums;
using EnergyMonitoringSystem.Service.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(DashboardService dashboardService, ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        /// <summary>
        /// Dashboard için Tüketim, Maliyet ve Emisyon verilerini tek seferde döner.
        /// Artık ön hesaplamalı verileri (MeterConsumptions) kullandığı için çok hızlıdır.
        /// </summary>
        [HttpPost("data")]
        public async Task<IActionResult> GetDashboardData([FromBody] DashboardRequestDto request)
        {
            try
            {
                // 1. Basit Validasyonlar
                if (request.StartDate >= request.EndDate)
                    return BadRequest("Başlangıç tarihi bitiş tarihinden küçük olmalıdır.");

                // Mantıksız interval kontrolü (Örn: 1 yıllık veri için 15dk'lık interval istenmemeli)
                var totalDays = (request.EndDate - request.StartDate).TotalDays;
                if (totalDays > 31 && request.Interval == DashboardInterval.FifteenMinutes)
                {
                    return BadRequest("31 günden uzun raporlar için 15 dakikalık aralık seçilemez. Lütfen Saatlik veya Günlük seçin.");
                }

                // 2. Servisten veriyi al
                var data = await _dashboardService.GetDashboardData(request);

                return Ok(data);
            }
            catch (ArgumentException argEx)
            {
                // Kullanıcı hatası (Örn: Tenant seçilmedi)
                return BadRequest(new { Message = argEx.Message });
            }
            catch (Exception ex)
            {
                // Sunucu hatası
                _logger.LogError(ex, "Dashboard verisi çekilirken hata oluştu.");
                return StatusCode(500, new { Message = "Sunucu tarafında bir hata oluştu.", Detailed = ex.Message });
            }
        }
    }
}