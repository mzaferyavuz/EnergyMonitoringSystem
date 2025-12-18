using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.Entities
{
    public class CarbonFactor
    {
        public int Id { get; set; }
        public double Factor { get; set; } // kgCO2/kWh
        public DateTime ValidFrom { get; set; }
    }
}
