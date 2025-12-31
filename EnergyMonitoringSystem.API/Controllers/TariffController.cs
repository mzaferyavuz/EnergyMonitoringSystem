using EnergyMonitoringSystem.Core.Constants;
using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Authorize(Roles = RoleConstants.Admin)] // Sadece Admin finansal ayar yapabilir
    [Route("api/[controller]")]
    [ApiController]
    public class TariffController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TariffController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Tarifeleri Listele
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tariffs = await _context.Tariffs
                            .Include(t => t.Tenant)
                            .OrderByDescending(t => t.ValidFrom) // En yeni tarih en üstte
                            .Select(t => new TariffDto
                            {
                                Id = t.Id,
                                ValidFrom = t.ValidFrom,
                                // ValidTo ataması KALDIRILDI
                                UnitPrice = t.UnitPrice,
                                TenantId = t.TenantId,
                                TenantName = t.Tenant != null ? t.Tenant.Name : "Genel Tarife"
                            })
                            .AsNoTracking()
                            .ToListAsync();

            return Ok(tariffs);
        }

        // 2. Yeni Tarife Ekle (Çakışma Kontrollü)
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] TariffDto model)
        {

            // MANTIK DEĞİŞİKLİĞİ: Overlap yerine Duplicate kontrolü
            // Aynı Tenant için, AYNI BAŞLANGIÇ TARİHİNE sahip kayıt var mı?
            bool exists = await _context.Tariffs.AnyAsync(t =>
                t.TenantId == model.TenantId &&
                t.ValidFrom.Date == model.ValidFrom.Date // Saat farkını ihmal etmek istersen .Date kullan
            );

            if (exists)
            {
                return BadRequest("Bu başlangıç tarihi için zaten bir tarife girilmiş. Lütfen tarihi değiştirin veya mevcut kaydı güncelleyin.");
            }

            var tariff = new Tariff
            {
                ValidFrom = model.ValidFrom,
                // ValidTo ataması KALDIRILDI
                UnitPrice = model.UnitPrice,
                TenantId = model.TenantId
            };

            _context.Tariffs.Add(tariff);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Tarife eklendi." });
        }

        // 3. Tarife Silme
        // Not: Finansal verilerde silme işlemi tehlikelidir. Genelde "Soft Delete" veya yasaklama önerilir.
        // Ancak UI isteği üzerine silme koyuyoruz, fakat geçmişe dönük hesaplanmış faturaları bozabileceği için uyarı mesajı ile kullanılmalı.
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var tariff = await _context.Tariffs.FindAsync(id);
            if (tariff == null) return NotFound("Tarife bulunamadı.");

            _context.Tariffs.Remove(tariff);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Tarife silindi." });
        }
    }
}
