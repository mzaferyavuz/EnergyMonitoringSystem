using EnergyMonitoringSystem.Core.DTOs;
using EnergyMonitoringSystem.Core.Entities;
using EnergyMonitoringSystem.Service.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EnergyMonitoringSystem.Core.Constants;

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

            if (user == null) return Unauthorized("Kullanıcı bulunamadı.");

            if (!await _userManager.CheckPasswordAsync(user, loginDto.Password))
                return Unauthorized("Hatalı şifre.");

            var userRoles = await _userManager.GetRolesAsync(user);
            var role = userRoles.FirstOrDefault();

            if (string.IsNullOrEmpty(role))
                return Unauthorized("Kullanıcının rolü bulunamadı.");

            var roles = new List<string> { role! };

            var tokenResult =  _tokenService.CreateToken(user, roles);

            return Ok(new LoginResponseDto
            {
                Token = tokenResult.Token,
                Expiration = tokenResult.Expiration,
                UserId = user.Id,
                UserName = user.UserName, // Frontend'de "Merhaba user1" yazmak için
                Email = user.Email,
                Role = role
            });
        }

        [HttpPost("register")]
        [Authorize(Roles = RoleConstants.Admin)] // Sadece Admin yeni kullanıcı ekleyebilir
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
