using System;

namespace EnergyMonitoringSystem.Core.Helpers
{
    public static class ModbusHelper
    {
        public static double ConvertModbusData(ushort[] inputs, string dataType, string byteOrder)
        {
            if (inputs== null || inputs.Length < 1) return 0;

            byte[] finalBytes = new byte[4];

            if (dataType == "Float" || dataType == "Int32")
            {
                if (inputs.Length < 2) return 0;

                byte[] w1 = BitConverter.GetBytes(inputs[0]);
                byte[] w2 = BitConverter.GetBytes(inputs[1]);

                switch (byteOrder)
                {
                    case "BigEndian": // ABCD
                        finalBytes[0] = w2[0]; finalBytes[1] = w2[1];
                        finalBytes[2] = w1[0]; finalBytes[3] = w1[1];
                        break;
                    case "LittleEndian": // CDAB
                        finalBytes[0] = w1[0]; finalBytes[1] = w1[1];
                        finalBytes[2] = w2[0]; finalBytes[3] = w2[1];
                        break;
                    // BADC ve DCBA senaryoları gerekirse eklenir
                    default:
                        finalBytes[0] = w2[0]; finalBytes[1] = w2[1];
                        finalBytes[2] = w1[0]; finalBytes[3] = w1[1];
                        break;
                }

                if (dataType == "Float") return BitConverter.ToSingle(finalBytes, 0);
                if (dataType == "Int32") return BitConverter.ToInt32(finalBytes, 0);
            }

            else if (dataType == "Double" || dataType== "Int64")
            {
                // Double için 4 Register (4 x 16 bit = 64 bit) gerekir
                if (inputs.Length < 4) return 0;
                byte[] w1 = BitConverter.GetBytes(inputs[0]); // En Yüksek Word (BigEndian için)
                byte[] w2 = BitConverter.GetBytes(inputs[1]);
                byte[] w3 = BitConverter.GetBytes(inputs[2]);
                byte[] w4 = BitConverter.GetBytes(inputs[3]); // En Düşük Word

                byte[] finalBytes8 = new byte[8];

                switch (byteOrder)
                {
                    case "BigEndian": // (Siemens Enerji Sayaçları)
                        // Sıralama: w1(En Büyük) ... w4(En Küçük)
                        // LE Sistem için tam tersi dizmeliyiz: w4, w3, w2, w1
                        finalBytes8[0] = w4[0]; finalBytes8[1] = w4[1];
                        finalBytes8[2] = w3[0]; finalBytes8[3] = w3[1];
                        finalBytes8[4] = w2[0]; finalBytes8[5] = w2[1];
                        finalBytes8[6] = w1[0]; finalBytes8[7] = w1[1];
                        break;

                    case "LittleEndian":
                        // Sıralama: w1(En Küçük) ... w4(En Büyük)
                        finalBytes8[0] = w1[0]; finalBytes8[1] = w1[1];
                        finalBytes8[2] = w2[0]; finalBytes8[3] = w2[1];
                        finalBytes8[4] = w3[0]; finalBytes8[5] = w3[1];
                        finalBytes8[6] = w4[0]; finalBytes8[7] = w4[1];
                        break;

                    default: // Varsayılan BigEndian
                        finalBytes8[0] = w4[0]; finalBytes8[1] = w4[1];
                        finalBytes8[2] = w3[0]; finalBytes8[3] = w3[1];
                        finalBytes8[4] = w2[0]; finalBytes8[5] = w2[1];
                        finalBytes8[6] = w1[0]; finalBytes8[7] = w1[1];
                        break;
                }
                if (dataType == "Double") return BitConverter.ToDouble(finalBytes8, 0);
                if (dataType == "Int64") return BitConverter.ToInt64(finalBytes8, 0);
            }

            else
            {
                // 16-bit
                return inputs[0];
            }

            return 0;
        }
    }
}