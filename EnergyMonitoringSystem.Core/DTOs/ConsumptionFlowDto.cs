using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class ConsumptionFlowRequestDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<int>? TenantIds { get; set; } // Boş gelirse tüm saha
    }

    public class ConsumptionFlowNodeDto
    {
        public int MeterId { get; set; }
        public string MeterName { get; set; }

        // Frontend'de gruplama ve renklendirme için kritik
        public string UsagePurpose { get; set; }

        // Seçilen tarih aralığındaki TOPLAM tüketim
        public decimal TotalConsumption { get; set; }

        // Hiyerarşik olarak bu sayaca bağlı alt sayaçlar
        public List<ConsumptionFlowNodeDto> Children { get; set; } = new();
    }
}
