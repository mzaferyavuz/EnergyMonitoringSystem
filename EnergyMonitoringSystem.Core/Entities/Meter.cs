using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.Entities
{
    public class Meter
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsVirtual { get; set; }

        public double VirtualMultiplier { get; set; } = 1.0;

        // Hiyerarşi Özellikleri
        public int? ParentMeterId { get; set; }
        public virtual Meter ParentMeter { get; set; }
        public virtual ICollection<Meter> ChildMeters { get; set; }

        // İlişkiler
        public int? TenantId { get; set; }
        public virtual Tenant Tenant { get; set; }

        public int? PurposeId { get; set; }
        public virtual UsagePurpose Purpose { get; set; }

        public int? ModbusDeviceId { get; set; } // Sanal sayaç ise null olur
        public virtual ModbusDevice ModbusDevice { get; set; }
    }
}
