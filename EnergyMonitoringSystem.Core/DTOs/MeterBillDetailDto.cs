using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class MeterBillDetailDto
    {
        public int MeterId { get; set; }
        public string MeterName { get; set; }
        public string UsagePurpose { get; set; } // Örn: "Aydınlatma", "HVAC" (Varsa)

        // İlk Endeks Bilgileri
        public decimal? FirstIndex { get; set; }      // Veri yoksa null olabilir
        public DateTime? FirstIndexDate { get; set; } // Gerçek okuma zamanı

        // Son Endeks Bilgileri
        public decimal? LastIndex { get; set; }
        public DateTime? LastIndexDate { get; set; }

        // Hesaplamalar
        public decimal Consumption { get; set; } // Tüketim (kWh)
        public decimal Amount { get; set; }      // Tutar (TL)

        // Mantıksal Bilgi (Frontend'de gri göstermek veya dipnot düşmek için)
        public bool IsExcludedFromTotal { get; set; } // True ise toplama dahil edilmedi demektir
        public string Note { get; set; } // "Ana sayaca bağlı olduğu için toplama dahil edilmedi" vb.
    }
}
