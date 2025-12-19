using EnergyMonitoringSystem.Core.Constants;
using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ParameterController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ParameterController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetParameters()
        {
            return Ok(await _context.MeasurementParameters.ToListAsync());
        }

        [HttpPost]
        [Authorize(Roles = RoleConstants.Admin)] // Sadece Admin yeni parametre tanımlayabilir
        public async Task<IActionResult> AddParameter([FromBody] MeasurementParameter param)
        {
            _context.MeasurementParameters.Add(param);
            await _context.SaveChangesAsync();
            return Ok(param);
        }
    }
}
