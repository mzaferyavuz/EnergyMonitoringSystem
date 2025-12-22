using System;

namespace EnergyMonitoringSystem.Core.Helpers
{
    public static class ModbusHelper
    {
        public static double ConvertModbusData(ushort[] inputs, string dataType, string byteOrder)
        {
            if (inputs.Length < 1) return 0;

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
            else
            {
                // 16-bit
                return inputs[0];
            }

            return 0;
        }
    }
}