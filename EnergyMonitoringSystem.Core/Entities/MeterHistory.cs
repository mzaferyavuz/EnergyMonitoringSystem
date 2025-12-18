using EnergyMonitoringSystem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.Entities
{
    public class MeterHistory
    {
        public long Id { get; set; }
        public int MeterId { get; set; }
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public RegisterType Type { get; set; }
    }
}
