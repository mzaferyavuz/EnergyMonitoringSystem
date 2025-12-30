using EnergyMonitoringSystem.Core.Constants;
using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Authorize(Roles = RoleConstants.Admin)]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public UserController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // 1. Kullanıcı Listesi
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var userDtos = new List<UserListDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                string? tenantName = null;
                if (user.TenantId.HasValue)
                {
                    tenantName = await _context.Tenants
                        .Where(t => t.Id == user.TenantId)
                        .Select(t => t.Name)
                        .FirstOrDefaultAsync();
                }

                userDtos.Add(new UserListDto
                {
                    Id = user.Id,
                    UserName = user.UserName, // FullName yok
                    Email = user.Email,
                    Role = roles.FirstOrDefault() ?? "-",
                    TenantName = tenantName
                });
            }

            return Ok(userDtos);
        }

        // 2. Rolleri Getir (Dropdown)
        [HttpGet("roles")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            return Ok(roles);
        }

        // 3. Yeni Kullanıcı Ekle
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto model)
        {
            // UserName Kontrolü
            if (await _userManager.FindByNameAsync(model.UserName) != null)
                return BadRequest("Bu kullanıcı adı zaten alınmış.");

            // Email Kontrolü (Identity genelde unique email ister)
            if (await _userManager.FindByEmailAsync(model.Email) != null)
                return BadRequest("Bu e-posta adresi zaten kullanımda.");

            // Rol Kontrolü
            if (!await _roleManager.RoleExistsAsync(model.Role))
                return BadRequest("Geçersiz rol.");

            var user = new ApplicationUser
            {
                UserName = model.UserName, // Login için esas alan
                Email = model.Email,
                TenantId = model.TenantId
                // FullName ataması kaldırıldı
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);
                return Ok(new { Message = "Kullanıcı oluşturuldu." });
            }

            return BadRequest(result.Errors.Select(e => e.Description));
        }

        // 4. Kullanıcı Sil
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound("Kullanıcı bulunamadı.");

            if (User.Identity?.Name == user.UserName)
                return BadRequest("Kendi hesabınızı silemezsiniz.");

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded) return Ok(new { Message = "Kullanıcı silindi." });

            return BadRequest(result.Errors.Select(e => e.Description));
        }
    }
}