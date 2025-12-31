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
    [Authorize(Roles = RoleConstants.Admin)]
    [Route("api/[controller]")]
    [ApiController]
    public class CarbonFactorController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CarbonFactorController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var factors = await _context.CarbonFactors
                .OrderByDescending(f => f.ValidFrom)
                .Select(f => new CarbonFactorDto
                {
                    Id = f.Id,
                    ValidFrom = f.ValidFrom,
                    //ValidTo = f.ValidTo,
                    Factor = f.Factor
                })
                .AsNoTracking()
                .ToListAsync();

            return Ok(factors);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CarbonFactorDto model)
        {
            // Duplicate Kontrolü
            bool exists = await _context.CarbonFactors.AnyAsync(f =>
                f.ValidFrom.Date == model.ValidFrom.Date);

            if (exists)
                return BadRequest("Bu tarihte zaten bir emisyon faktörü tanımlı.");

            var factor = new CarbonFactor
            {
                ValidFrom = model.ValidFrom,
                // ValidTo KALDIRILDI
                Factor = model.Factor
            };

            _context.CarbonFactors.Add(factor);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Emisyon faktörü eklendi." });

        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var factor = await _context.CarbonFactors.FindAsync(id);
            if (factor == null) return NotFound();

            _context.CarbonFactors.Remove(factor);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Kayıt silindi." });
        }
    }
}
