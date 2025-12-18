using EnergyMonitoringSystem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.Entities
{
    public class ModbusRegister
    {
        public int Id { get; set; }
        public int ModbusDeviceId { get; set; }
        public virtual ModbusDevice ModbusDevice { get; set; }

        public string Name { get; set; } // Örn: L1 Gerilim
        public int RegisterAddress { get; set; } // Örn: 30005
        public RegisterType Type { get; set; } // Enum: kWh, Voltage vb.
        public string DataType { get; set; } // Float, Int32, Double vb.
        public double ScaleFactor { get; set; } = 1.0; // Gelen veriyi çarpmak için (Örn: 0.1)
    }
}
