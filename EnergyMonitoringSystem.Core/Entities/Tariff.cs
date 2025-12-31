using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.Entities
{
    public class Tariff
    {
        public int Id { get; set; }
        public int? TenantId { get; set; } // Null ise "Tenant'ı olmayan sayaçlar" içindir
        public decimal UnitPrice { get; set; }
        public DateTime ValidFrom { get; set; } // Bu tarihten itibaren geçerli

        [ForeignKey("TenantId")]
        public virtual Tenant? Tenant { get; set; }
    }
}
