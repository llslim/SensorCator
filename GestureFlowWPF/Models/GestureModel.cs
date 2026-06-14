using System.Collections.Generic;

namespace GestureFlowWPF.Models
{
    public class GestureTemplate
    {
        public string Name { get; set; } = string.Empty;
        public List<double[]> Points { get; set; } = new List<double[]>();
        public double Threshold { get; set; } = 15.0; // Default matching distance threshold
        public bool UseGyro { get; set; } = false; // By default, match using Accelerometer (3D) to be robust
    }
}
