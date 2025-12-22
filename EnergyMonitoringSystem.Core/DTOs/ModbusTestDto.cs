using System.ComponentModel.DataAnnotations;

namespace EnergyMonitoringSystem.Core.DTOs
{
    public class ModbusTestRequestDto
    {
        [Required]
        public string IpAddress { get; set; } // Örn: 192.168.1.10

        public int Port { get; set; } = 502;

        public byte SlaveId { get; set; } = 1; // Unit ID

        [Required]
        public ushort RegisterAddress { get; set; } // Örn: 30005

        public string DataType { get; set; } = "Float"; // Float, Int32, UInt16

        public string ByteOrder { get; set; } = "BigEndian"; // BigEndian, LittleEndian

        public double ScaleFactor { get; set; } = 1.0;
    }
}