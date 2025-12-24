using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class BillReportDto
    {
        public int MeterId { get; set; }
        public string MeterName { get; set; }

        // İlk Okuma Bilgileri
        public decimal FirstIndex { get; set; }
        public DateTime FirstIndexDate { get; set; }

        // Son Okuma Bilgileri
        public decimal LastIndex { get; set; }
        public DateTime LastIndexDate { get; set; }

        // Hesaplama Detayları
        public decimal TotalConsumption { get; set; } // Tüketim (kWh)
        public decimal UnitPrice { get; set; }        // Birim Fiyat (TL)
        public decimal TotalAmount { get; set; }      // Fatura Tutarı (TL)
        public string Currency { get; set; } = "TL";

        // Hata veya Bilgi mesajı için (Örn: "Veri bulunamadı")
        public string Message { get; set; }
        public bool IsSuccess { get; set; } = true;
    }
}
