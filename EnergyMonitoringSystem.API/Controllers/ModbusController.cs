using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EnergyMonitoringSystem.Core.Constants;

namespace EnergyMonitoringSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleConstants.Admin+"," +RoleConstants.Standard)] // Admin ve Standard cihaz yönetebilir
    public class ModbusController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ModbusController(AppDbContext context)
        {
            _context = context;
        }

        // Madde 1: Yeni Modbus Cihazı Ekleme
        [HttpPost("device")]
        public async Task<IActionResult> AddDevice([FromBody] ModbusDevice device)
        {
            _context.ModbusDevices.Add(device);
            await _context.SaveChangesAsync();
            return Ok(device);
        }

        // Madde 2: Cihaza Register (kWh, Akım, Voltaj) Tanımlama
        [HttpPost("register")]
        public async Task<IActionResult> AddRegister([FromBody] ModbusRegister reg)
        {
            _context.ModbusRegisters.Add(reg);
            await _context.SaveChangesAsync();
            return Ok(reg);
        }

        [HttpGet("devices")]
        public async Task<IActionResult> GetDevices()
        {
            var devices = await _context.ModbusDevices.ToListAsync();
            return Ok(devices);
        }
    }
}
