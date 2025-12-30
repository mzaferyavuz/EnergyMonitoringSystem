using EnergyMonitoringSystem.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class AnalysisRequestDto
    {
        public List<int> MeterIds { get; set; } = new();
        public List<int> ParameterIds { get; set; } = new(); // Voltaj, Akım, Enerji vb. ID'leri
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Null gelirse "Özet Modu" (Toplam/Ortalama) çalışır.
        // Dolu gelirse "Zaman Serisi Modu" çalışır.
        public DashboardInterval? Interval { get; set; }
    }

    public class AnalysisResponseDto
    {
        // Grafiklerde X ekseni için (Sadece Interval seçiliyse dolar)
        public List<string> Labels { get; set; } = new();

        public List<MeterAnalysisDto> Meters { get; set; } = new();
    }

    public class MeterAnalysisDto
    {
        public int MeterId { get; set; }
        public string MeterName { get; set; }
        public string UsagePurpose { get; set; } // "Aydınlatma", "Other" vb.

        // Bu sayacın parametre bazlı verileri
        public List<ParameterAnalysisResultDto> Parameters { get; set; } = new();
    }

    public class ParameterAnalysisResultDto
    {
        public int ParameterId { get; set; }
        public string ParameterName { get; set; } // "L1 Voltage", "Active Energy"
        public string Unit { get; set; }          // "V", "kWh"

        // Özet Değer (Interval yoksa bu gösterilir, varsa genel toplam/ortalama)
        public decimal SummaryValue { get; set; }

        // Zaman Serisi Verileri (Interval varsa dolar)
        public List<decimal?> SeriesData { get; set; } = new();
    }
}
