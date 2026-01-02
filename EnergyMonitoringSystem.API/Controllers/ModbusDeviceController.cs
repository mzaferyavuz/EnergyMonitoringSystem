using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Authorize] // Güvenlik için
    [Route("api/[controller]")]
    [ApiController]
    public class ModbusDeviceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ModbusDeviceController(AppDbContext context)
        {
            _context = context;
        }

        // 1. LİSTELEME
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var devices = await _context.ModbusDevices
                .AsNoTracking()
                .OrderBy(d => d.DeviceName)
                .ToListAsync();
            return Ok(devices);
        }

        // 2. DETAY (ID ile Getir)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var device = await _context.ModbusDevices.FindAsync(id);
            if (device == null) return NotFound("Cihaz bulunamadı.");
            return Ok(device);
        }

        // 3. EKLEME
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ModbusDevice model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // IP çakışması kontrolü (Opsiyonel ama iyi olur)
            if (await _context.ModbusDevices.AnyAsync(d => d.IpAddress == model.IpAddress))
                return BadRequest("Bu IP adresine sahip bir cihaz zaten var.");

            _context.ModbusDevices.Add(model);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Modbus cihazı eklendi.", Id = model.Id });
        }

        // 4. GÜNCELLEME
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ModbusDevice model)
        {
            var device = await _context.ModbusDevices.FindAsync(id);
            if (device == null) return NotFound("Cihaz bulunamadı.");

            device.DeviceName = model.DeviceName;
            device.IpAddress = model.IpAddress;
            device.Port = model.Port;
            //device.Description = model.Description;
            device.IsActive = model.IsActive;

            // Eğer UnitId veya diğer teknik detaylar varsa buraya ekleyin

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Modbus cihazı güncellendi." });
        }

        // 5. SİLME (Kritik Kontrol Burada)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var device = await _context.ModbusDevices
                .Include(d => d.Meters) // İlişkili sayaçları kontrol etmek için Include şart
                .FirstOrDefaultAsync(d => d.Id == id);

            if (device == null) return NotFound("Cihaz bulunamadı.");

            // KURAL: Meter'ı olan cihaz silinemez.
            if (device.Meters != null && device.Meters.Any())
            {
                return BadRequest($"Bu cihaza bağlı {device.Meters.Count} adet sayaç var. Önce sayaçları silmelisiniz.");
            }

            _context.ModbusDevices.Remove(device);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Modbus cihazı silindi." });
        }
    }
}