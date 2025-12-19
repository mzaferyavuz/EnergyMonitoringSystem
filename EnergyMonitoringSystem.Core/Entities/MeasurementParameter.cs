using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.Entities
{
    public class MeasurementParameter
    {
        public int Id { get; set; }

        // Örn: "Aktif Enerji", "L1 Voltajı", "Toplam Reaktif Güç"
        public string Name { get; set; }

        // Kod içinde mantıksal kontrol gerekirse diye (Örn: "ACTIVE_ENERGY", "REACTIVE_POWER")
        public string Key { get; set; }

        // Örn: "kWh", "V", "kVAr", "Hz"
        public string Unit { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
