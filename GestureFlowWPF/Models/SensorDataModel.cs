namespace GestureFlowWPF.Models
{
    public class SensorDataModel
    {
        public long Timestamp { get; set; }
        public double AccX { get; set; }
        public double AccY { get; set; }
        public double AccZ { get; set; }
        public double GyroX { get; set; }
        public double GyroY { get; set; }
        public double GyroZ { get; set; }
        public double MagX { get; set; }
        public double MagY { get; set; }
        public double MagZ { get; set; }

        public override string ToString()
        {
            return $"{Timestamp},{AccX:F6},{AccY:F6},{AccZ:F6},{GyroX:F6},{GyroY:F6},{GyroZ:F6},{MagX:F6},{MagY:F6},{MagZ:F6}";
        }

        public static string GetCsvHeader()
        {
            return "Timestamp,acc_x,acc_y,acc_z,gyro_x,gyro_y,gyro_z,magn_x,magn_y,magn_z";
        }
    }
}
