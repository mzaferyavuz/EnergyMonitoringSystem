using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class TenantBillDto
    {
        // --- FATURA ÜST BİLGİLERİ (HEADER) ---
        public int TenantId { get; set; }
        public string TenantName { get; set; }
        public string TaxNumber { get; set; }
        public DateTime FilterStartDate { get; set; } // Kullanıcının seçtiği başlangıç
        public DateTime FilterEndDate { get; set; }   // Kullanıcının seçtiği bitiş

        // --- FİNANSAL TOPLAMLAR ---
        public decimal TotalConsumption { get; set; } // Toplam Tüketim (kWh)
        public decimal UnitPrice { get; set; }        // Birim Fiyat (Tenant Fiyatı)
        public decimal TotalAmount { get; set; }      // Toplam Fatura Tutarı
        public string Currency { get; set; } = "TL";

        // --- DETAY LİSTESİ ---
        public List<MeterBillDetailDto> MeterDetails { get; set; } = new List<MeterBillDetailDto>();
    }
}
