using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.Entities
{
    public class MeterConsumption
    {
        public long Id { get; set; }
        public int MeterId { get; set; }
        public DateTime Timestamp { get; set; } // Örn: 14:15:00 (Saniye her zaman 00)

        // O periyottaki NET tüketim (kWh)
        public decimal Consumption { get; set; }

        // "15Min" veya "Hourly"
        public string PeriodType { get; set; }

        // İleride join atmadan hızlıca tarife/maliyet hesaplamak istersen buraya UnitPrice da eklenebilir
        // Ama şimdilik sadece tüketim tutalım.
    }
}
