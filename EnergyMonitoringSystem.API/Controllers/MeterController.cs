using EnergyMonitoringSystem.Core.Constants;
using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Authorize(Roles = RoleConstants.Admin)]
    [Route("api/[controller]")]
    [ApiController]
    public class MeterController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MeterController(AppDbContext context)
        {
            _context = context;
        }

        // --- 1. LİSTELEME ---
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var meters = await _context.Meters
                .Include(m => m.ModbusDevice)
                .Include(m => m.Purpose)
                .Include(m => m.ParentMeter)
                .Include(m => m.ModbusRegisters)
                .Select(m => new MeterListDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    DeviceName = m.ModbusDevice != null ? m.ModbusDevice.DeviceName : "-",
                    ParentName = m.ParentMeter != null ? m.ParentMeter.Name : "-",
                    UsagePurpose = m.Purpose != null ? m.Purpose.Name : "-",
                    IsVirtual = m.IsVirtual,
                    RegisterCount = m.ModbusRegisters.Count
                })
                .AsNoTracking()
                .ToListAsync();

            return Ok(meters);
        }

        // --- 2. DETAY GETİR (EDIT İÇİN) ---
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var meter = await _context.Meters
                .Include(m => m.ModbusRegisters)
                .Include(m => m.Purpose)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (meter == null) return NotFound("Sayaç bulunamadı.");

            var dto = new MeterCreateUpdateDto
            {
                Name = meter.Name,
                //SerialNumber = meter.SerialNumber,
                ModbusDeviceId = meter.ModbusDeviceId ?? 0,
                ParentMeterId = meter.ParentMeterId,
                UsagePurposeId = meter.Purpose.Id,
                TenantId = meter.TenantId,
                IsVirtual = meter.IsVirtual,
                VirtualMultiplier = meter.VirtualMultiplier,
                Registers = meter.ModbusRegisters.Select(r => new MeterRegisterDto
                {
                    Id = r.Id,
                    MeasurementParameterId = r.MeasurementParameterId,
                    RegisterAddress = r.RegisterAddress,
                    DataType = r.DataType,
                    ScaleFactor = r.ScaleFactor,
                    ByteOrder = r.ByteOrder // DB'deki "BigEndian" vb. değeri döner
                }).ToList()
            };

            return Ok(dto);
        }

        // --- 3. EKLEME ---
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MeterCreateUpdateDto model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var meter = new Meter
                {
                    Name = model.Name,
                    //SerialNumber = model.SerialNumber,
                    ModbusDeviceId = model.ModbusDeviceId,
                    ParentMeterId = model.ParentMeterId,
                    PurposeId = model.UsagePurposeId,
                    TenantId = model.TenantId,
                    IsVirtual = model.IsVirtual,
                    VirtualMultiplier = model.VirtualMultiplier
                };

                _context.Meters.Add(meter);
                await _context.SaveChangesAsync();

                if (model.Registers != null && model.Registers.Any())
                {
                    foreach (var regDto in model.Registers)
                    {
                        var register = new ModbusRegister
                        {
                            MeterId = meter.Id,
                            MeasurementParameterId = regDto.MeasurementParameterId,
                            RegisterAddress = regDto.RegisterAddress,
                            DataType = regDto.DataType,
                            ScaleFactor = regDto.ScaleFactor,
                            ByteOrder = regDto.ByteOrder // "BigEndian" vb.
                        };
                        _context.ModbusRegisters.Add(register);
                    }
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return Ok(new { Message = "Sayaç eklendi.", MeterId = meter.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest($"Hata: {ex.Message}");
            }
        }

        // --- 4. GÜNCELLEME ---
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] MeterCreateUpdateDto model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var meter = await _context.Meters
                    .Include(m => m.ModbusRegisters)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (meter == null) return NotFound("Sayaç bulunamadı.");

                meter.Name = model.Name;
                //meter.SerialNumber = model.SerialNumber;
                meter.ModbusDeviceId = model.ModbusDeviceId;
                meter.ParentMeterId = model.ParentMeterId;
                meter.PurposeId = model.UsagePurposeId;
                meter.TenantId = model.TenantId;
                meter.IsVirtual = model.IsVirtual;
                meter.VirtualMultiplier = model.VirtualMultiplier;

                // Register Senkronizasyonu
                var existingRegisters = meter.ModbusRegisters.ToList();
                var incomingRegisters = model.Registers ?? new List<MeterRegisterDto>();

                // Silinecekler
                var toDelete = existingRegisters
                    .Where(e => !incomingRegisters.Any(i => i.Id == e.Id))
                    .ToList();
                _context.ModbusRegisters.RemoveRange(toDelete);

                // Eklenecek ve Güncellenecekler
                foreach (var inc in incomingRegisters)
                {
                    if (inc.Id > 0)
                    {
                        var existing = existingRegisters.FirstOrDefault(e => e.Id == inc.Id);
                        if (existing != null)
                        {
                            existing.RegisterAddress = inc.RegisterAddress;
                            existing.MeasurementParameterId = inc.MeasurementParameterId;
                            existing.DataType = inc.DataType;
                            existing.ScaleFactor = inc.ScaleFactor;
                            existing.ByteOrder = inc.ByteOrder; // Güncelleme
                        }
                    }
                    else
                    {
                        var newReg = new ModbusRegister
                        {
                            MeterId = meter.Id,
                            RegisterAddress = inc.RegisterAddress,
                            MeasurementParameterId = inc.MeasurementParameterId,
                            DataType = inc.DataType,
                            ScaleFactor = inc.ScaleFactor,
                            ByteOrder = inc.ByteOrder // Ekleme
                        };
                        _context.ModbusRegisters.Add(newReg);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { Message = "Sayaç güncellendi." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest($"Hata: {ex.Message}");
            }
        }

        // --- 5. SİLME ---
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var meter = await _context.Meters.FindAsync(id);
            if (meter == null) return NotFound("Sayaç bulunamadı.");

            _context.Meters.Remove(meter);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Sayaç silindi." });
        }

        // --- LOOKUP METOTLARI ---

        [HttpGet("lookup/parents")]
        public async Task<IActionResult> GetParentLookup()
        {
            var parents = await _context.Meters
                .Select(m => new { m.Id, m.Name })
                .AsNoTracking()
                .ToListAsync();
            return Ok(parents);
        }

        [HttpGet("lookup/devices")]
        public async Task<IActionResult> GetDeviceLookup()
        {
            var devices = await _context.ModbusDevices
                .Select(d => new { d.Id, Name = $"{d.DeviceName} ({d.IpAddress}:{d.Port})" })
                .AsNoTracking()
                .ToListAsync();
            return Ok(devices);
        }

        [HttpGet("lookup/purposes")]
        public async Task<IActionResult> GetPurposeLookup()
        {
            var purposes = await _context.UsagePurposes
                .Select(p => new { p.Id, p.Name })
                .AsNoTracking()
                .ToListAsync();
            return Ok(purposes);
        }

        // YENİ EKLENDİ: Background Service ile Uyumlu ByteOrder Listesi
        // Frontend'de Dropdown olarak gösterilecek.
        [HttpGet("lookup/byteorders")]
        public IActionResult GetByteOrderLookup()
        {
            // Bu listeyi Background Service'indeki switch-case yapısıyla birebir aynı yapıyoruz.
            var byteOrders = new List<string>
            {
                "BigEndian",
                "LittleEndian",
                "BigEndianByteSwap",
                "LittleEndianByteSwap"
            };

            return Ok(byteOrders);
        }
    }
}