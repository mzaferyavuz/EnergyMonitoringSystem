using EnergyMonitoringSystem.Core.Constants;
using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Core.Helpers;
using EnergyMonitoringSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NModbus;
using System.Net.Sockets;

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


        [HttpPost("test-live-read")]
        [Authorize(Roles = RoleConstants.Admin)] // Sadece Admin test yapabilsin
        public async Task<IActionResult> TestLiveRead([FromBody] ModbusTestRequestDto request)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    // 1. Bağlantı Testi
                    var connectTask = client.ConnectAsync(request.IpAddress, request.Port);
                    if (await Task.WhenAny(connectTask, Task.Delay(3000)) != connectTask)
                    {
                        return BadRequest("Cihaza bağlanılamadı (Timeout - 3sn). IP ve Port'u kontrol edin.");
                    }

                    // 2. Modbus Master Oluştur
                    var factory = new ModbusFactory();
                    IModbusMaster master = factory.CreateMaster(client);
                    master.Transport.ReadTimeout = 2000; // Okuma zaman aşımı

                    // 3. Okunacak Boyut
                    ushort pointsToRead = 1;
                    // if (request.DataType == "Float" || request.DataType == "Int32") pointsToRead = 2;
                    switch (request.DataType)
                    {
                        case "Double":  // 64-bit = 4 Register
                        case "Int64":   // 64-bit = 4 Register
                            pointsToRead = 4;
                            break;
                        case "Float":   // 32-bit = 2 Register
                        case "Int32":   // 32-bit = 2 Register
                            pointsToRead = 2;
                            break;
                        default:        // 16-bit (Int16, UInt16 vb.)
                            pointsToRead = 1;
                            break;
                    }

                    // 4. Canlı Okuma
                    ushort[] inputs = await master.ReadHoldingRegistersAsync(request.SlaveId, request.RegisterAddress, pointsToRead);

                    // 5. Dönüştürme (Helper kullanıyoruz)
                    double rawValue = ModbusHelper.ConvertModbusData(inputs, request.DataType, request.ByteOrder);
                    double finalValue = rawValue * request.ScaleFactor;

                    return Ok(new
                    {
                        Success = true,
                        RawInputs = inputs, // Gelen ham ushort değerleri (Debug için)
                        CalculatedValue = finalValue,
                        Message = "Okuma Başarılı"
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Error = ex.Message });
            }
        }

    }
}
