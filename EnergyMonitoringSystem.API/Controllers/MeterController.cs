using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EnergyMonitoringSystem.Core.Constants;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Bu controller'daki her şey için giriş yapmış olmak şart
    public class MeterController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MeterController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Madde 15: External kullanıcıların (Tenant) sadece kendi sayaçlarını görmesi
        [HttpGet("my-meters")]
        [Authorize(Roles = RoleConstants.External)] // Sadece External rolündekiler girebilir
        public async Task<IActionResult> GetTenantMeters()
        {
            // 1. Giriş yapan kullanıcının ID'sini sistemden al (Token içinden gelir)
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            // 2. Kullanıcıyı veritabanından çek (TenantId bilgisi için)
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.TenantId == null) return NotFound("Tenant bilgisi bulunamadı.");

            // 3. Sadece bu Tenant'a ait sayaçları filtrele
            var meters = await _context.Meters
                .Where(m => m.TenantId == user.TenantId)
                .ToListAsync();

            return Ok(meters);
        }

        // Madde 12: Admin'in tüm sayaçları listelemesi
        [HttpGet("all-meters")]
        [Authorize(Roles = RoleConstants.Admin)]
        public async Task<IActionResult> GetAllMeters()
        {
            var meters = await _context.Meters.Include(m => m.Tenant).ToListAsync();
            return Ok(meters);
        }
    }
}
