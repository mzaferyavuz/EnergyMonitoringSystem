using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class UserListDto
    {

        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string? TenantName { get; set; } // Bağlı olduğu tenant varsa adı
    }
}
