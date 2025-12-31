using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class TariffDto
    {
        public int Id { get; set; } // Güncelleme için gerekli

        [Required]
        public DateTime ValidFrom { get; set; } // Başlangıç Tarihi


        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Fiyat 0'dan küçük olamaz.")]
        public decimal UnitPrice { get; set; }  // Birim Fiyat (TL)

        public int? TenantId { get; set; }      // Hangi kiracı için? (Boş ise Genel Tarife)
        public string? TenantName { get; set; } // Listeleme yaparken UI'da göstermek için
    }
}
