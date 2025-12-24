using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly DashboardService _dashboardService;

        public DashboardController(DashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// Dashboard için Tüketim, Maliyet ve Emisyon verilerini tek seferde döner.
        /// Interval kısıtlamalarına dikkat edilmelidir (Örn: 3 ay için 15dk seçilmemeli).
        /// </summary>
        [HttpPost("data")]
        public async Task<IActionResult> GetDashboardData([FromBody] DashboardRequestDto request)
        {
            try
            {
                // 1. Basit Validasyonlar
                if (request.StartDate >= request.EndDate)
                    return BadRequest("Başlangıç tarihi bitiş tarihinden küçük olmalıdır.");

                // 2. Servisten veriyi al
                var data = await _dashboardService.GetDashboardData(request);

                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }
    }
}