using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        public string Username { get; set; }
       
        [Required(ErrorMessage = "Şifre zorunludur.")]
        public string Password { get; set; }
    }

    public class RegisterDto
    {
        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "E-posta adresi zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
        public string Password { get; set; }
        public string Role { get; set; } // Admin, Standard, ViewOnly, External
        public int? TenantId { get; set; } // Sadece External kullanıcılar için
    }
}
