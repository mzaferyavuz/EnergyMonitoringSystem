using EnergyMonitoringSystem.Service.Services;
using EnergyMonitoringSystem.Core.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConsumptionFlowController : ControllerBase
    {
        private readonly ConsumptionFlowService _flowService;
        private readonly ILogger<ConsumptionFlowController> _logger;

        public ConsumptionFlowController(ConsumptionFlowService flowService, ILogger<ConsumptionFlowController> logger)
        {
            _flowService = flowService;
            _logger = logger;
        }

        /// <summary>
        /// Tüketim Akış Diyagramı (Sankey/Tree) için hiyerarşik veri döner.
        /// Veriler Parent -> Child ilişkisine göre iç içe (nested) JSON formatındadır.
        /// </summary>
        /// <param name="request">Başlangıç-Bitiş tarihi ve Opsiyonel Tenant ID listesi</param>
        [HttpPost("get-flow")]
        public async Task<IActionResult> GetConsumptionFlow([FromBody] ConsumptionFlowRequestDto request)
        {
            // Loglama: İsteğin geldiğini kaydet
            _logger.LogInformation("Consumption Flow isteği alındı. Start: {Start}, End: {End}, TenantCount: {Count}",
                request.StartDate, request.EndDate, request.TenantIds?.Count ?? 0);

            try
            {
                // 1. Validasyonlar
                if (request.StartDate >= request.EndDate)
                {
                    _logger.LogWarning("Geçersiz tarih aralığı ile istek yapıldı.");
                    return BadRequest("Başlangıç tarihi bitiş tarihinden küçük olmalıdır.");
                }

                // 2. Servis Çağrısı
                var flowData = await _flowService.GetConsumptionFlow(request);

                // 3. Veri Kontrolü
                if (flowData == null || !flowData.Any())
                {
                    // Veri yoksa boş liste yerine 200 OK ve boş dizi dönmek frontend için daha iyidir.
                    return Ok(new List<ConsumptionFlowNodeDto>());
                }

                return Ok(flowData);
            }
            catch (Exception ex)
            {
                // Exception Middleware olsa bile kritik noktalarda özel loglama iyidir.
                _logger.LogError(ex, "Consumption Flow endpointinde beklenmeyen hata.");
                return StatusCode(500, new { Message = "Sunucu hatası oluştu.", Detail = ex.Message });
            }
        }

    }
}
