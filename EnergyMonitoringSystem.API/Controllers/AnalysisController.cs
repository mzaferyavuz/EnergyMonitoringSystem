using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Service.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AnalysisController : ControllerBase
    {
        private readonly AnalysisService _analysisService;
        private readonly ILogger<AnalysisController> _logger;

        public AnalysisController(AnalysisService analysisService, ILogger<AnalysisController> logger)
        {
            _analysisService = analysisService;
            _logger = logger;
        }

        /// <summary>
        /// Gelişmiş analiz endpoint'i. Çoklu sayaç ve parametre desteği sunar.
        /// Consumption verilerini hesaplanmış tablodan (SUM), diğerlerini ham veriden (SNAPSHOT/AVG) çeker.
        /// </summary>
        [HttpPost("get-data")]
        public async Task<IActionResult> GetAnalysisData([FromBody] AnalysisRequestDto request)
        {
            try
            {
                // Basit Validasyonlar
                if (request.MeterIds == null || !request.MeterIds.Any())
                    return BadRequest("En az bir sayaç seçilmelidir.");

                if (request.ParameterIds == null || !request.ParameterIds.Any())
                    return BadRequest("En az bir parametre seçilmelidir.");

                if (request.StartDate >= request.EndDate)
                    return BadRequest("Başlangıç tarihi bitiş tarihinden küçük olmalıdır.");

                var result = await _analysisService.GetAnalysisData(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Analiz verisi çekilirken hata oluştu.");
                return StatusCode(500, new { Message = "Sunucu hatası", Detail = ex.Message });
            }
        }
    }
}
