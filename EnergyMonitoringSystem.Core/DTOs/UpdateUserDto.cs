using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class UpdateUserDto
    {
        public string UserName { get; set; }
        public string Email { get; set; }

        // Şifre alanı boş gelebilir, boşsa değiştirme demektir.
        public string? Password { get; set; }

        public string Role { get; set; }
        public int? TenantId { get; set; }
    }
}
