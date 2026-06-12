using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using GestureFlowWPF.Models;
using GestureFlowWPF.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace GestureFlowWPF.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly BluetoothService _btService;
        private readonly CsvLogger _csvLogger;
        private readonly string _baseFolder;
        private readonly string _rawFolder;
        private readonly string _trimmedFolder;

        private DispatcherTimer? _recordingTimer;
        private DateTime _recordingStartTime;

        // Telemetry properties
        private double _accX;
        private double _accY;
        private double _accZ;
        private double _gyroX;
        private double _gyroY;
        private double _gyroZ;
        private double _magX;
        private double _magY;
        private double _magZ;

        // UI states
        private string _statusText = "Disconnected";
        private string _scanButtonText = "Start Scan";
        private string _recordButtonText = "Start Recording";
        private string _recordTimeText = "00:00";
        private bool _isConnected;
        private bool _isScanning;
        private bool _isRecording;
        private DiscoveredDevice? _selectedDevice;
        private string? _selectedLogFile;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = new ObservableCollection<DiscoveredDevice>();
        public ObservableCollection<string> LogFiles { get; } = new ObservableCollection<string>();

        // Charting fields
        private ISeries[] _chartSeries = Array.Empty<ISeries>();
        private Axis[] _xAxes = Array.Empty<Axis>();
        private Axis[] _yAxes = Array.Empty<Axis>();

        public MainWindowViewModel()
        {
            _btService = new BluetoothService();
            _csvLogger = new CsvLogger();

            // Setup local folders under MyDocuments/SensorCator
            _baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SensorCator");
            _rawFolder = Path.Combine(_baseFolder, "rawData");
            _trimmedFolder = Path.Combine(_baseFolder, "trimmedData");

            EnsureFoldersExist();

            // Wire up Bluetooth events
            _btService.DeviceDiscovered += BtService_DeviceDiscovered;
            _btService.DataReceived += BtService_DataReceived;
            _btService.StatusChanged += BtService_StatusChanged;

            // Load existing logs
            RefreshLogFiles();

            // Initialize chart empty state
            ClearChart();
        }

        private void EnsureFoldersExist()
        {
            if (!Directory.Exists(_baseFolder)) Directory.CreateDirectory(_baseFolder);
            if (!Directory.Exists(_rawFolder)) Directory.CreateDirectory(_rawFolder);
            if (!Directory.Exists(_trimmedFolder)) Directory.CreateDirectory(_trimmedFolder);
            if (!Directory.Exists(Path.Combine(_baseFolder, "models"))) Directory.CreateDirectory(Path.Combine(_baseFolder, "models"));
            if (!Directory.Exists(Path.Combine(_baseFolder, "rawVideos"))) Directory.CreateDirectory(Path.Combine(_baseFolder, "rawVideos"));
        }

        public void RefreshLogFiles()
        {
            LogFiles.Clear();
            if (Directory.Exists(_rawFolder))
            {
                var files = Directory.GetFiles(_rawFolder, "*.csv")
                                     .Select(Path.GetFileName)
                                     .Where(f => f != null)
                                     .OrderByDescending(f => f)
                                     .Cast<string>();

                foreach (var file in files)
                {
                    LogFiles.Add(file);
                }
            }
        }

        // Properties binding
        public double AccX { get => _accX; set => SetField(ref _accX, value); }
        public double AccY { get => _accY; set => SetField(ref _accY, value); }
        public double AccZ { get => _accZ; set => SetField(ref _accZ, value); }
        public double GyroX { get => _gyroX; set => SetField(ref _gyroX, value); }
        public double GyroY { get => _gyroY; set => SetField(ref _gyroY, value); }
        public double GyroZ { get => _gyroZ; set => SetField(ref _gyroZ, value); }
        public double MagX { get => _magX; set => SetField(ref _magX, value); }
        public double MagY { get => _magY; set => SetField(ref _magY, value); }
        public double MagZ { get => _magZ; set => SetField(ref _magZ, value); }

        public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }
        public string ScanButtonText { get => _scanButtonText; set => SetField(ref _scanButtonText, value); }
        public string RecordButtonText { get => _recordButtonText; set => SetField(ref _recordButtonText, value); }
        public string RecordTimeText { get => _recordTimeText; set => SetField(ref _recordTimeText, value); }

        public bool IsConnected { get => _isConnected; set => SetField(ref _isConnected, value); }
        public bool IsScanning { get => _isScanning; set => SetField(ref _isScanning, value); }
        public bool IsRecording { get => _isRecording; set => SetField(ref _isRecording, value); }

        public DiscoveredDevice? SelectedDevice
        {
            get => _selectedDevice;
            set => SetField(ref _selectedDevice, value);
        }

        public string? SelectedLogFile
        {
            get => _selectedLogFile;
            set
            {
                if (SetField(ref _selectedLogFile, value))
                {
                    LoadSelectedLogToChart();
                }
            }
        }

        public ISeries[] ChartSeries { get => _chartSeries; set => SetField(ref _chartSeries, value); }
        public Axis[] XAxes { get => _xAxes; set => SetField(ref _xAxes, value); }
        public Axis[] YAxes { get => _yAxes; set => SetField(ref _yAxes, value); }

        // Scanning Command handler
        public void ToggleScanning()
        {
            if (IsScanning)
            {
                _btService.StopScanning();
                IsScanning = false;
                ScanButtonText = "Start Scan";
            }
            else
            {
                DiscoveredDevices.Clear();
                _btService.StartScanning();
                IsScanning = true;
                ScanButtonText = "Stop Scan";
            }
        }

        // Connect/Disconnect Command handler
        public async Task ToggleConnectionAsync()
        {
            if (IsConnected)
            {
                if (IsRecording) StopRecording();
                await _btService.DisconnectAsync();
                IsConnected = false;
                SelectedDevice = null;
            }
            else
            {
                if (SelectedDevice == null)
                {
                    StatusText = "Please select a device from the list first.";
                    return;
                }

                bool success = await _btService.ConnectAsync(SelectedDevice.BluetoothAddress);
                if (success)
                {
                    IsConnected = true;
                    // Subscribe to IMU9 at 52Hz
                    await _btService.SubscribeImuAsync("/Meas/IMU9/52");
                }
            }
        }

        // Recording Command handler
        public void ToggleRecording()
        {
            if (IsRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            if (!IsConnected) return;

            string filePath = _csvLogger.StartLogging(_rawFolder);
            IsRecording = true;
            RecordButtonText = "Stop Recording";
            StatusText = $"Recording to {Path.GetFileName(filePath)}...";

            _recordingStartTime = DateTime.Now;
            _recordingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _recordingTimer.Tick += RecordingTimer_Tick;
            _recordingTimer.Start();
        }

        private void StopRecording()
        {
            if (!IsRecording) return;

            _csvLogger.StopLogging();
            IsRecording = false;
            RecordButtonText = "Start Recording";
            StatusText = "Recording saved.";

            if (_recordingTimer != null)
            {
                _recordingTimer.Stop();
                _recordingTimer.Tick -= RecordingTimer_Tick;
                _recordingTimer = null;
            }
            RecordTimeText = "00:00";

            RefreshLogFiles();
        }

        private void RecordingTimer_Tick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.Now - _recordingStartTime;
            RecordTimeText = string.Format("{0:D2}:{1:D2}", (int)elapsed.TotalMinutes, elapsed.Seconds);
        }

        // Process CSV Command
        public void ProcessSelectedCsv()
        {
            if (string.IsNullOrEmpty(SelectedLogFile))
            {
                MessageBox.Show("Please select a log file to process.", "No Log Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string sourcePath = Path.Combine(_rawFolder, SelectedLogFile);
            if (!File.Exists(sourcePath)) return;

            try
            {
                var processor = new CsvFileProcessor(sourcePath);
                
                // Run calculations
                processor.ApplyTime();
                processor.ApplyMovingAverage();
                processor.ApplyDifferentiation();

                string destPath = Path.Combine(_trimmedFolder, "processed_" + SelectedLogFile);
                processor.Save(destPath);

                MessageBox.Show($"File processed successfully and saved to trimmedData/processed_{SelectedLogFile}\nCalculated Time indices, Accel Moving Average, and Accel Differentiation.", 
                    "Processing Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing CSV file: {ex.Message}", "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // LiveCharts load method
        private void LoadSelectedLogToChart()
        {
            if (string.IsNullOrEmpty(SelectedLogFile))
            {
                ClearChart();
                return;
            }

            string filePath = Path.Combine(_rawFolder, SelectedLogFile);
            if (!File.Exists(filePath))
            {
                ClearChart();
                return;
            }

            try
            {
                var processor = new CsvFileProcessor(filePath);

                int accXIdx = processor.GetColumnIndex("acc_x");
                int accYIdx = processor.GetColumnIndex("acc_y");
                int accZIdx = processor.GetColumnIndex("acc_z");

                if (accXIdx == -1 || accYIdx == -1 || accZColMatches(processor))
                {
                    // Check fallback columns if column headers do not match
                    accXIdx = 1;
                    accYIdx = 2;
                    accZIdx = 3;
                }

                var accXValues = new List<double>();
                var accYValues = new List<double>();
                var accZValues = new List<double>();

                for (int i = 1; i < processor.CsvData.Count; i++)
                {
                    var row = processor.CsvData[i];
                    if (accXIdx < row.Length && double.TryParse(row[accXIdx], out double x)) accXValues.Add(x);
                    if (accYIdx < row.Length && double.TryParse(row[accYIdx], out double y)) accYValues.Add(y);
                    if (accZIdx < row.Length && double.TryParse(row[accZIdx], out double z)) accZValues.Add(z);
                }

                ChartSeries = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Name = "Acc X",
                        Values = accXValues,
                        Fill = null,
                        Stroke = new SolidColorPaint(SKColors.Crimson, 2.0f),
                        GeometrySize = 0,
                        LineSmoothness = 0.5
                    },
                    new LineSeries<double>
                    {
                        Name = "Acc Y",
                        Values = accYValues,
                        Fill = null,
                        Stroke = new SolidColorPaint(SKColors.MediumSeaGreen, 2.0f),
                        GeometrySize = 0,
                        LineSmoothness = 0.5
                    },
                    new LineSeries<double>
                    {
                        Name = "Acc Z",
                        Values = accZValues,
                        Fill = null,
                        Stroke = new SolidColorPaint(SKColors.RoyalBlue, 2.0f),
                        GeometrySize = 0,
                        LineSmoothness = 0.5
                    }
                };

                XAxes = new Axis[]
                {
                    new Axis
                    {
                        Name = "Sample Index",
                        Labeler = val => val.ToString("N0")
                    }
                };

                YAxes = new Axis[]
                {
                    new Axis
                    {
                        Name = "Acceleration (m/s²)",
                        Labeler = val => val.ToString("F2")
                    }
                };
            }
            catch (Exception ex)
            {
                StatusText = $"Chart error: {ex.Message}";
                ClearChart();
            }
        }

        private bool accZColMatches(CsvFileProcessor p)
        {
            return p.GetColumnIndex("acc_z") == -1;
        }

        private void ClearChart()
        {
            ChartSeries = Array.Empty<ISeries>();
            XAxes = new Axis[] { new Axis { Name = "Sample Index" } };
            YAxes = new Axis[] { new Axis { Name = "Acceleration (m/s²)" } };
        }

        // Bluetooth event handlers
        private void BtService_DeviceDiscovered(DiscoveredDevice device)
        {
            // Marshall to UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!DiscoveredDevices.Any(d => d.MacAddress == device.MacAddress))
                {
                    DiscoveredDevices.Add(device);
                }
            });
        }

        private void BtService_DataReceived(SensorDataModel data)
        {
            // Marshall to UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                AccX = data.AccX;
                AccY = data.AccY;
                AccZ = data.AccZ;
                GyroX = data.GyroX;
                GyroY = data.GyroY;
                GyroZ = data.GyroZ;
                MagX = data.MagX;
                MagY = data.MagY;
                MagZ = data.MagZ;

                if (IsRecording)
                {
                    _csvLogger.LogData(data);
                }
            });
        }

        private void BtService_StatusChanged(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = message;
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
