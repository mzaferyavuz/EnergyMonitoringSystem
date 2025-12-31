using EnergyMonitoringSystem.Core.Constants;
using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Authorize(Roles = RoleConstants.Admin)] // Sadece Admin yönetebilir
    [ApiController]
    [Route("api/[controller]")]
    public class TenantController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TenantController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTenant([FromBody] Tenant tenant)
        {
            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();
            return Ok(tenant);
        }

        // Madde 9: Tarife Tanımlama
        [HttpPost("tariff")]
        public async Task<IActionResult> AddTariff([FromBody] Tariff tariff)
        {
            _context.Tariffs.Add(tariff);
            await _context.SaveChangesAsync();
            return Ok(tariff);
        }

        /// <summary>
        /// Bir kiracıya atanmamış (boşta) olan sayaçları ve 
        /// parametre olarak gönderilen kiracıya ait olanları döner.
        /// (Transfer List sol tarafı için)
        /// </summary>
        [HttpGet("{tenantId}/available-meters")]
        public async Task<IActionResult> GetAvailableMetersForTenant(int tenantId)
        {
            var meters = await _context.Meters
                .Where(m => m.TenantId == null || m.TenantId == tenantId) // Boşta olanlar + Bu kiracınınkiler
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    //m.SerialNumber,
                    IsAssignedToCurrent = m.TenantId == tenantId // UI'da işaretli göstermek için
                })
                .ToListAsync();

            return Ok(meters);
        }

        /// <summary>
        /// Bir kiracıya toplu sayaç atama işlemi.
        /// Listede olmayan sayaçların ataması kaldırılır (Sync Mantığı).
        /// </summary>
        [HttpPost("{tenantId}/assign-meters")]
        public async Task<IActionResult> AssignMeters(int tenantId, [FromBody] List<int> meterIds)
        {
            // 1. Kiracı var mı kontrol et
            var tenant = await _context.Tenants.FindAsync(tenantId);
            if (tenant == null) return NotFound("Kiracı bulunamadı.");

            // 2. Bu kiracının mevcut sayaçlarını bul
            var currentMeters = await _context.Meters.Where(m => m.TenantId == tenantId).ToListAsync();

            // 3. Atamaları Temizle (Listede OLMAYANLARI boşa çıkar)
            foreach (var meter in currentMeters)
            {
                if (!meterIds.Contains(meter.Id))
                {
                    meter.TenantId = null; // Kiracıdan sök
                }
            }

            // 4. Yeni Atamaları Yap (Listede OLANLARI ata)
            // Sadece boşta olanları veya zaten bu kiracının olanları güncelliyoruz.
            // Başka kiracının sayacını çalmayı engellemek için "TenantId == null" kontrolü eklenebilir.
            var newMeters = await _context.Meters
                .Where(m => meterIds.Contains(m.Id))
                .ToListAsync();

            foreach (var meter in newMeters)
            {
                // Güvenlik: Başka bir kiracıya aitse hata ver veya atla.
                // Biz şimdilik yönetici yetkisi olduğu için direkt atıyoruz (Override).
                meter.TenantId = tenantId;
            }

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Sayaç atamaları güncellendi." });
        }
    }
}
