using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnergyMonitoringSystem.Core.Entities
{
    public class ModbusRegister
    {
        public int Id { get; set; }

        // ESKİ: ModbusDeviceId (Sildik, çünkü register sayaca aittir)
        // YENİ: MeterId
        public int MeterId { get; set; }
        public virtual Meter Meter { get; set; }

        public int MeasurementParameterId { get; set; }
        public virtual MeasurementParameter MeasurementParameter { get; set; }

        public int RegisterAddress { get; set; }

        // Float, Int32, Int16 vb.
        public string DataType { get; set; }

        // YENİ: Veri Dizilimi (BigEndian, LittleEndian, BigEndianByteSwap, LittleEndianByteSwap)
        // Varsayılan: "BigEndian" (Standart Modbus)
        public string ByteOrder { get; set; } = "BigEndian";

        public double ScaleFactor { get; set; } = 1.0;
    }
}
