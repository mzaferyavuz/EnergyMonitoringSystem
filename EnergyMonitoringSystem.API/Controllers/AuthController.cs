using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Service.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMonitoringSystem.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ITokenService _tokenService;

        public AuthController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ITokenService tokenService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var user = await _userManager.FindByNameAsync(loginDto.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, loginDto.Password))
            {
                var roles = await _userManager.GetRolesAsync(user);
                var token = _tokenService.CreateToken(user, roles);
                return Ok(new { Token = token });
            }
            return Unauthorized();
        }

        [HttpPost("register")]
        [Authorize(Roles = "Admin")] // Sadece Admin yeni kullanıcı ekleyebilir
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            var user = new ApplicationUser
            {
                UserName = registerDto.Username,
                Email = registerDto.Email,
                TenantId = registerDto.TenantId // Madde 15: Tenant bağlama
            };

            var result = await _userManager.CreateAsync(user, registerDto.Password);

            if (result.Succeeded)
            {
                // Rol atama işlemi
                if (!string.IsNullOrEmpty(registerDto.Role))
                {
                    await _userManager.AddToRoleAsync(user, registerDto.Role);
                }
                return Ok("Kullanıcı başarıyla oluşturuldu.");
            }
            return BadRequest(result.Errors);
        }
    }
}
