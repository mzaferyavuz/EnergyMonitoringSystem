using System.ComponentModel.DataAnnotations;

namespace EnergyMonitoringSystem.Core.DTOs
{
    // Register Ekleme/Düzenleme DTO
    public class MeterRegisterDto
    {
        public int Id { get; set; }

        [Required]
        public int MeasurementParameterId { get; set; }

        [Required]
        public int RegisterAddress { get; set; }

        [Required]
        public string DataType { get; set; } = "Float"; // Float, Int32, UInt16 vb.

        public double ScaleFactor { get; set; } = 1.0;

        // DÜZELTME: "ABCD" yerine "BigEndian" varsayılan yapıldı.
        // Arka plandaki switch-case yapın muhtemelen şunları bekliyor:
        // "BigEndian", "LittleEndian", "BigEndianByteSwap", "LittleEndianByteSwap"
        [Required]
        public string ByteOrder { get; set; } = "BigEndian";
    }

    // Sayaç Ekleme/Düzenleme DTO
    public class MeterCreateUpdateDto
    {
        [Required]
        public string Name { get; set; }

        public string? SerialNumber { get; set; }

        [Required]
        public int ModbusDeviceId { get; set; }

        public int? ParentMeterId { get; set; }
        public int? UsagePurposeId { get; set; }
        public int? TenantId { get; set; }

        public bool IsVirtual { get; set; }
        public double VirtualMultiplier { get; set; } = 1.0;

        public List<MeterRegisterDto> Registers { get; set; } = new();
    }

    // Listeleme DTO
    public class MeterListDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string DeviceName { get; set; }
        public string? ParentName { get; set; }

        public string UsagePurpose { get; set; }
        public bool IsVirtual { get; set; }
        public int RegisterCount { get; set; }
    }
}