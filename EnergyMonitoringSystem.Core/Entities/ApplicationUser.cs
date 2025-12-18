using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace EnergyMonitoringSystem.Core.Entities
{
    public class ApplicationUser : IdentityUser
    {
        // Madde 15 için: Eğer kullanıcı bir Tenant ise hangi tenant olduğunu tutarız.
        public int? TenantId { get; set; }
        public virtual Tenant Tenant { get; set; }
    }
}
