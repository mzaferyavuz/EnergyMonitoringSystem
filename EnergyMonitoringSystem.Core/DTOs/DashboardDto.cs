using EnergyMonitoringSystem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class DashboardRequestDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DashboardInterval Interval { get; set; }

        public DashboardScope Scope { get; set; }
        public List<int>? TenantIds { get; set; } // Scope == Tenant ise dolu olmalı
    }

    public class DashboardResponseDto
    {
        // Grafiklerde X ekseni olacak tarihler
        public List<string> Labels { get; set; } = new();

        // Her bir kullanım amacı için (Örn: Aydınlatma, HVAC) ayrı seri
        public List<DashboardSeriesDto> Series { get; set; } = new();

        // Kartlarda gösterilecek genel toplamlar
        public decimal TotalConsumption { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalEmission { get; set; }
    }

    public class DashboardSeriesDto
    {
        public string PurposeName { get; set; } // Örn: "HVAC"
        public List<decimal> ConsumptionData { get; set; } = new(); // kWh
        public List<decimal> CostData { get; set; } = new();        // TL
        public List<decimal> EmissionData { get; set; } = new();    // kgCO2
    }
}
