using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GestureFlowWPF.Models;

namespace GestureFlowWPF.Services
{
    public class CsvLogger
    {
        private StreamWriter? _writer;
        private string _filePath = string.Empty;

        public bool IsLogging => _writer != null;

        public string StartLogging(string folderPath)
        {
            if (IsLogging) StopLogging();

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            string fileName = $"{timestamp}.csv";
            _filePath = Path.Combine(folderPath, fileName);

            _writer = new StreamWriter(_filePath, false, Encoding.UTF8);
            // Write the CSV header
            _writer.WriteLine(SensorDataModel.GetCsvHeader());
            _writer.Flush();

            return _filePath;
        }

        public void LogData(SensorDataModel data)
        {
            if (_writer == null) return;
            _writer.WriteLine(data.ToString());
        }

        public void StopLogging()
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Close();
                _writer.Dispose();
                _writer = null;
            }
        }
    }

    public class CsvFileProcessor
    {
        public List<string[]> CsvData { get; private set; } = new List<string[]>();
        public string FilePath { get; private set; } = string.Empty;

        public CsvFileProcessor() { }

        public CsvFileProcessor(string path)
        {
            FilePath = path;
            Load(path);
        }

        public void Load(string path)
        {
            CsvData.Clear();
            if (!File.Exists(path)) return;

            using (var reader = new StreamReader(path))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var row = line.Split(',');
                    for (int i = 0; i < row.Length; i++)
                    {
                        row[i] = row[i].Trim();
                    }
                    CsvData.Add(row);
                }
            }
        }

        public void Save(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                foreach (var row in CsvData)
                {
                    writer.WriteLine(string.Join(",", row));
                }
            }
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                throw new InvalidOperationException("No file path specified for saving.");
            }
            Save(FilePath);
        }

        public int GetRowCount() => CsvData.Count;

        public string[] GetHeader() => CsvData.Count > 0 ? CsvData[0] : Array.Empty<string>();

        public List<string> GetColumnData(int columnIndex)
        {
            var list = new List<string>();
            for (int i = 1; i < CsvData.Count; i++)
            {
                if (columnIndex < CsvData[i].Length)
                {
                    list.Add(CsvData[i][columnIndex]);
                }
                else
                {
                    list.Add(string.Empty);
                }
            }
            return list;
        }

        public List<string> GetColumnData(string headerName)
        {
            int colIdx = GetColumnIndex(headerName);
            if (colIdx == -1)
            {
                throw new KeyNotFoundException($"Header '{headerName}' not found.");
            }
            return GetColumnData(colIdx);
        }

        public int GetColumnIndex(string headerName)
        {
            if (CsvData.Count == 0) return -1;
            string[] header = CsvData[0];
            for (int i = 0; i < header.Length; i++)
            {
                if (header[i].Equals(headerName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        public void ApplyTime()
        {
            if (CsvData.Count <= 1) return;

            // Frequency of 50 Hz/52 Hz -> Android app does `i / 50.0`
            for (int i = 1; i < CsvData.Count; i++)
            {
                var row = CsvData[i];
                double time = (double)i / 50.0;
                Array.Resize(ref row, row.Length + 1);
                row[row.Length - 1] = time.ToString("F3");
                CsvData[i] = row;
            }

            var header = CsvData[0];
            Array.Resize(ref header, header.Length + 1);
            header[header.Length - 1] = "Time";
            CsvData[0] = header;
        }

        public void TrimData(int startTimeMs, int endTimeMs)
        {
            if (CsvData.Count <= 1) return;

            double startSec = startTimeMs / 1000.0;
            double endSec = endTimeMs / 1000.0;

            int timeCol = GetColumnIndex("Time");
            if (timeCol == -1)
            {
                ApplyTime();
                timeCol = GetColumnIndex("Time");
            }

            var trimmed = new List<string[]>();
            trimmed.Add(CsvData[0]); // Header

            for (int i = 1; i < CsvData.Count; i++)
            {
                var row = CsvData[i];
                if (timeCol < row.Length && double.TryParse(row[timeCol], out double t))
                {
                    if (t >= startSec && t <= endSec)
                    {
                        trimmed.Add(row);
                    }
                }
            }

            CsvData = trimmed;
        }

        public void ApplyMovingAverage()
        {
            if (CsvData.Count <= 1) return;

            int accXCol = GetColumnIndex("acc_x");
            int accYCol = GetColumnIndex("acc_y");
            int accZCol = GetColumnIndex("acc_z");

            if (accXCol == -1 || accYCol == -1 || accZCol == -1) return;

            var accX = new List<double>();
            var accY = new List<double>();
            var accZ = new List<double>();

            for (int i = 1; i < CsvData.Count; i++)
            {
                accX.Add(double.TryParse(CsvData[i][accXCol], out double x) ? x : 0.0);
                accY.Add(double.TryParse(CsvData[i][accYCol], out double y) ? y : 0.0);
                accZ.Add(double.TryParse(CsvData[i][accZCol], out double z) ? z : 0.0);
            }

            int windowSize = 2;
            var maX = CalculateMovingAverage(accX, windowSize);
            var maY = CalculateMovingAverage(accY, windowSize);
            var maZ = CalculateMovingAverage(accZ, windowSize);

            for (int i = 1; i < CsvData.Count; i++)
            {
                var row = CsvData[i];
                int originalLength = row.Length;
                Array.Resize(ref row, originalLength + 3);

                row[originalLength] = maX[i - 1]?.ToString("F6") ?? accX[i - 1].ToString("F6");
                row[originalLength + 1] = maY[i - 1]?.ToString("F6") ?? accY[i - 1].ToString("F6");
                row[originalLength + 2] = maZ[i - 1]?.ToString("F6") ?? accZ[i - 1].ToString("F6");

                CsvData[i] = row;
            }

            var header = CsvData[0];
            int originalHeaderLength = header.Length;
            Array.Resize(ref header, originalHeaderLength + 3);
            header[originalHeaderLength] = "acc_ma_x";
            header[originalHeaderLength + 1] = "acc_ma_y";
            header[originalHeaderLength + 2] = "acc_ma_z";
            CsvData[0] = header;
        }

        private List<double?> CalculateMovingAverage(List<double> data, int windowSize)
        {
            var movingAvg = new List<double?>();
            for (int i = 0; i < data.Count; i++)
            {
                if (i < windowSize - 1)
                {
                    movingAvg.Add(null);
                }
                else
                {
                    double sum = 0.0;
                    for (int j = i; j > i - windowSize; j--)
                    {
                        sum += data[j];
                    }
                    movingAvg.Add(sum / windowSize);
                }
            }
            return movingAvg;
        }

        public void ApplyDifferentiation()
        {
            if (CsvData.Count <= 1) return;

            int accXCol = GetColumnIndex("acc_x");
            int accYCol = GetColumnIndex("acc_y");
            int accZCol = GetColumnIndex("acc_z");

            if (accXCol == -1 || accYCol == -1 || accZCol == -1) return;

            var accX = new List<double>();
            var accY = new List<double>();
            var accZ = new List<double>();

            for (int i = 1; i < CsvData.Count; i++)
            {
                accX.Add(double.TryParse(CsvData[i][accXCol], out double x) ? x : 0.0);
                accY.Add(double.TryParse(CsvData[i][accYCol], out double y) ? y : 0.0);
                accZ.Add(double.TryParse(CsvData[i][accZCol], out double z) ? z : 0.0);
            }

            var diffX = CalculateDifferences(accX);
            var diffY = CalculateDifferences(accY);
            var diffZ = CalculateDifferences(accZ);

            for (int i = 1; i < CsvData.Count; i++)
            {
                var row = CsvData[i];
                int originalLength = row.Length;
                Array.Resize(ref row, originalLength + 3);

                row[originalLength] = diffX[i - 1]?.ToString("F6") ?? accX[i - 1].ToString("F6");
                row[originalLength + 1] = diffY[i - 1]?.ToString("F6") ?? accY[i - 1].ToString("F6");
                row[originalLength + 2] = diffZ[i - 1]?.ToString("F6") ?? accZ[i - 1].ToString("F6");

                CsvData[i] = row;
            }

            var header = CsvData[0];
            int originalHeaderLength = header.Length;
            Array.Resize(ref header, originalHeaderLength + 3);
            header[originalHeaderLength] = "acc_diff_x";
            header[originalHeaderLength + 1] = "acc_diff_y";
            header[originalHeaderLength + 2] = "acc_diff_z";
            CsvData[0] = header;
        }

        private List<double?> CalculateDifferences(List<double> data)
        {
            var differences = new List<double?>();
            differences.Add(null);
            for (int i = 1; i < data.Count; i++)
            {
                differences.Add(data[i] - data[i - 1]);
            }
            return differences;
        }

        public List<double[]> GetSlicedDataPoints(int startTimeMs, int endTimeMs)
        {
            var list = new List<double[]>();
            if (CsvData.Count <= 1) return list;

            double startSec = startTimeMs / 1000.0;
            double endSec = endTimeMs / 1000.0;

            int timeCol = GetColumnIndex("Time");
            if (timeCol == -1)
            {
                ApplyTime();
                timeCol = GetColumnIndex("Time");
            }

            int accXCol = GetColumnIndex("acc_x");
            int accYCol = GetColumnIndex("acc_y");
            int accZCol = GetColumnIndex("acc_z");
            int gyroXCol = GetColumnIndex("gyro_x");
            int gyroYCol = GetColumnIndex("gyro_y");
            int gyroZCol = GetColumnIndex("gyro_z");

            // Fallback indices if headers are missing
            if (accXCol == -1) accXCol = 1;
            if (accYCol == -1) accYCol = 2;
            if (accZCol == -1) accZCol = 3;
            if (gyroXCol == -1) gyroXCol = 4;
            if (gyroYCol == -1) gyroYCol = 5;
            if (gyroZCol == -1) gyroZCol = 6;

            for (int i = 1; i < CsvData.Count; i++)
            {
                var row = CsvData[i];
                if (timeCol < row.Length && double.TryParse(row[timeCol], out double t))
                {
                    if (t >= startSec && t <= endSec)
                    {
                        double ax = accXCol < row.Length && double.TryParse(row[accXCol], out double valAx) ? valAx : 0.0;
                        double ay = accYCol < row.Length && double.TryParse(row[accYCol], out double valAy) ? valAy : 0.0;
                        double az = accZCol < row.Length && double.TryParse(row[accZCol], out double valAz) ? valAz : 0.0;
                        
                        double gx = gyroXCol < row.Length && double.TryParse(row[gyroXCol], out double valGx) ? valGx : 0.0;
                        double gy = gyroYCol < row.Length && double.TryParse(row[gyroYCol], out double valGy) ? valGy : 0.0;
                        double gz = gyroZCol < row.Length && double.TryParse(row[gyroZCol], out double valGz) ? valGz : 0.0;

                        list.Add(new double[] { ax, ay, az, gx, gy, gz });
                    }
                }
            }

            return list;
        }
    }
}
