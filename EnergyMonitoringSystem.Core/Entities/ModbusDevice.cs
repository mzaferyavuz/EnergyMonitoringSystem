using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.Entities
{
    public class ModbusDevice
    {
        public int Id { get; set; }
        public string DeviceName { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; } = 502;
        public byte UnitId { get; set; } // Slave ID
        public bool IsActive { get; set; }

        [JsonIgnore]
        public virtual ICollection<Meter> Meters { get; set; }
    }
}
