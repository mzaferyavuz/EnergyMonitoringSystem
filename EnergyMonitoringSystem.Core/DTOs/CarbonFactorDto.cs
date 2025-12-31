using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class CarbonFactorDto
    {
        public int Id { get; set; }

        [Required]
        public DateTime ValidFrom { get; set; }


        [Required]
        public double Factor { get; set; } // kgCO2/kWh
    }
}
