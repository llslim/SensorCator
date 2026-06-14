using System;
using System.Diagnostics;
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
using System.Windows.Media.Imaging;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

using System.Collections.Generic;

namespace GestureFlowWPF.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly BluetoothService _btService;
        private readonly CsvLogger _csvLogger;
        private readonly CameraService _cameraService;
        private readonly string _baseFolder;
        private readonly string _rawFolder;
        private readonly string _trimmedFolder;

        private DispatcherTimer? _recordingTimer;
        private DateTime _recordingStartTime;

        private DispatcherTimer? _playbackTimer;
        private double _currentPlaySec;
        private bool _isPlaying;
        private string _playButtonText = "Play";
        private string _playbackStatusText = "";

        private readonly MediaPipeClientService _mediaPipeClientService;
        private readonly GestureCatalogService _gestureCatalogService;

        // AAC fields
        private readonly AacStorageService _aacStorageService;
        private readonly TtsSpeechService _ttsSpeechService;
        private readonly System.Windows.Media.MediaPlayer _mediaPlayer = new System.Windows.Media.MediaPlayer();
        
        private AacSettings _aacSettings = new AacSettings();
        private string _speechDisplayText = string.Empty;
        private ActivityModel? _activeActivity;
        private bool _isSettingsVisible;
        private int _settingsTabIdx;
        private List<string> _availableVoices = new List<string>();
        private ObservableCollection<VocabularyCard> _activeCards = new ObservableCollection<VocabularyCard>();

        // CRUD fields for settings
        private string _newCardText = "";
        private string _newCardAudio = "";
        private string _newCardIcon = "";
        private string _newCardGesture = "";
        private VocabularyCard? _selectedCardForEdit;
        private string _newActivityName = "";
        private string _newDictOriginal = "";
        private string _newDictPhonetic = "";
        private ObservableCollection<HeatmapCell> _accuracyMatrix = new ObservableCollection<HeatmapCell>();
        private ObservableCollection<System.Windows.Point> _handLandmarkPoints = new ObservableCollection<System.Windows.Point>();
        private ObservableCollection<GestureTemplate> _gestureTemplates = new ObservableCollection<GestureTemplate>();
        private bool _imuGestureOnlyMode;
        private string _detectedGestureName = "None";
        private string _newGestureName = "";
        private double _newGestureThreshold = 15.0;
        private bool _newGestureUseGyro = false;
        private int _imuMatchCounter = 0;
        private readonly List<double[]> _sensorRollingBuffer = new List<double[]>();

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
        private WriteableBitmap? _liveCameraPreview;

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
            _cameraService = new CameraService();
            _mediaPipeClientService = new MediaPipeClientService();
            _gestureCatalogService = new GestureCatalogService();

            // Setup local folders under MyDocuments/SensorCator
            _baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SensorCator");
            _rawFolder = Path.Combine(_baseFolder, "rawData");
            _trimmedFolder = Path.Combine(_baseFolder, "trimmedData");

            EnsureFoldersExist();

            // Wire up Bluetooth events
            _btService.DeviceDiscovered += BtService_DeviceDiscovered;
            _btService.DataReceived += BtService_DataReceived;
            _btService.StatusChanged += BtService_StatusChanged;

            // Wire up Camera events
            _cameraService.PreviewFrameAvailable += CameraService_PreviewFrameAvailable;
            _cameraService.StatusChanged += CameraService_StatusChanged;

            _mediaPipeClientService.StatusChanged += (msg) => { StatusText = msg; };
            _mediaPipeClientService.HandLandmarksReceived += MediaPipeClientService_HandLandmarksReceived;

            // Load existing logs
            RefreshLogFiles();

            // Initialize chart empty state
            ClearChart();

            // Refresh templates
            RefreshGestureTemplates();

            // Initialize AAC
            _aacStorageService = new AacStorageService();
            _ttsSpeechService = new TtsSpeechService();

            // Load AAC configurations
            _aacSettings = _aacStorageService.LoadSettings();
            AllCards = new ObservableCollection<VocabularyCard>(_aacStorageService.LoadCards());
            AllActivities = new ObservableCollection<ActivityModel>(_aacStorageService.LoadActivities(AllCards.ToList()));
            PronunciationDictionary = new ObservableCollection<PronunciationItem>(_aacStorageService.LoadDictionary());

            AvailableVoices = _ttsSpeechService.GetInstalledVoices();
            if (string.IsNullOrEmpty(_aacSettings.TtsVoiceName) && AvailableVoices.Count > 0)
            {
                _aacSettings.TtsVoiceName = AvailableVoices[0];
            }

            // Set default active activity
            if (AllActivities.Count > 0)
            {
                ActiveActivity = AllActivities[0];
            }

            // Wire up TTS events
            _ttsSpeechService.WordSpoken += (s, args) => WordSpoken?.Invoke(this, args);
        }

        public void StartCamera()
        {
            if (!ImuGestureOnlyMode)
            {
                // Run camera init on a background thread — OpenCV native calls can crash
                // the process if they run on the UI thread when no camera is present.
                Task.Run(() =>
                {
                    try { _cameraService.StartPreview(0); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Camera start failed: {ex.Message}"); }
                });

                Task.Run(() =>
                {
                    try { _mediaPipeClientService.Start(0); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"MediaPipe start failed: {ex.Message}"); }
                });
            }
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
                    InitializeTrimmer();
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
            _cameraService.StartRecording(Path.Combine(_baseFolder, "rawVideos"));
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
            _cameraService.StopRecording();
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
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (!DiscoveredDevices.Any(d => d.MacAddress == device.MacAddress))
                {
                    DiscoveredDevices.Add(device);
                }
            });
        }

        private void BtService_DataReceived(SensorDataModel data)
        {
            if (AacSettings != null && !AacSettings.SensorInputActive) return;

            // Marshall to UI thread
            Application.Current?.Dispatcher.BeginInvoke(() =>
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

                // Add to rolling sensor buffer for DTW matching
                _sensorRollingBuffer.Add(new double[] { data.AccX, data.AccY, data.AccZ, data.GyroX, data.GyroY, data.GyroZ });
                if (_sensorRollingBuffer.Count > 150)
                {
                    _sensorRollingBuffer.RemoveAt(0);
                }

                // Attempt to match gesture 5 times a second (every 10 samples)
                _imuMatchCounter++;
                if (_imuMatchCounter % 10 == 0)
                {
                    _imuMatchCounter = 0;
                    PerformLiveGestureMatching();
                }
            });
        }

        private void BtService_StatusChanged(string message)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                StatusText = message;
            });
        }

        // Camera live preview property
        public WriteableBitmap? LiveCameraPreview
        {
            get => _liveCameraPreview;
            set => SetField(ref _liveCameraPreview, value);
        }

        // Trimmer Properties
        private double _trimStartSec;
        public double TrimStartSec
        {
            get => _trimStartSec;
            set
            {
                double val = Math.Max(0, Math.Min(value, TrimMaxSec));
                if (val > TrimEndSec) val = TrimEndSec;
                if (SetField(ref _trimStartSec, val))
                {
                    UpdateTrimmerPreview();
                }
                OnPropertyChanged(nameof(TrimStartSec));
            }
        }

        private double _trimEndSec;
        public double TrimEndSec
        {
            get => _trimEndSec;
            set
            {
                double val = Math.Max(0, Math.Min(value, TrimMaxSec));
                if (val < TrimStartSec) val = TrimStartSec;
                if (SetField(ref _trimEndSec, val))
                {
                    UpdateTrimmerPreview();
                }
                OnPropertyChanged(nameof(TrimEndSec));
            }
        }

        private double _trimMaxSec = 10.0;
        public double TrimMaxSec
        {
            get => _trimMaxSec;
            set => SetField(ref _trimMaxSec, value);
        }

        private WriteableBitmap? _trimVideoPreview;
        public WriteableBitmap? TrimVideoPreview
        {
            get => _trimVideoPreview;
            set => SetField(ref _trimVideoPreview, value);
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetField(ref _isPlaying, value);
        }

        public string PlayButtonText
        {
            get => _playButtonText;
            set => SetField(ref _playButtonText, value);
        }

        public string PlaybackStatusText
        {
            get => _playbackStatusText;
            set => SetField(ref _playbackStatusText, value);
        }

        public void TogglePlayback()
        {
            if (string.IsNullOrEmpty(SelectedLogFile)) return;
            string videoPath = Path.Combine(_baseFolder, "rawVideos", Path.ChangeExtension(SelectedLogFile, ".mp4"));
            if (!File.Exists(videoPath))
            {
                MessageBox.Show("No video file found for this log run to play.", "Video Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsPlaying)
            {
                StopPlayback();
            }
            else
            {
                StartPlayback(videoPath);
            }
        }

        private void StartPlayback(string videoPath)
        {
            IsPlaying = true;
            PlayButtonText = "Stop";
            _currentPlaySec = TrimStartSec;
            PlaybackStatusText = $"Playing: {_currentPlaySec:F2}s / {TrimEndSec:F2}s";

            _playbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
            };
            _playbackTimer.Tick += (s, e) =>
            {
                _currentPlaySec += 0.033;
                if (_currentPlaySec >= TrimEndSec)
                {
                    StopPlayback();
                    return;
                }

                PlaybackStatusText = $"Playing: {_currentPlaySec:F2}s / {TrimEndSec:F2}s";
                var frame = _cameraService.GetFrameAt(videoPath, (int)(_currentPlaySec * 1000.0));
                if (frame != null)
                {
                    TrimVideoPreview = frame;
                }
            };
            _playbackTimer.Start();
        }

        public void StopPlayback()
        {
            if (!IsPlaying) return;

            if (_playbackTimer != null)
            {
                _playbackTimer.Stop();
                _playbackTimer = null;
            }

            IsPlaying = false;
            PlayButtonText = "Play";
            PlaybackStatusText = "";
            UpdateTrimmerPreview(); // Reset preview to start point
        }

        private void CameraService_PreviewFrameAvailable(WriteableBitmap bitmap)
        {
            LiveCameraPreview = bitmap;
        }

        private void CameraService_StatusChanged(string msg)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                StatusText = msg;
            });
        }

        private void InitializeTrimmer()
        {
            StopPlayback();
            if (string.IsNullOrEmpty(SelectedLogFile))
            {
                TrimStartSec = 0;
                TrimEndSec = 0;
                TrimMaxSec = 10;
                TrimVideoPreview = null;
                return;
            }

            string filePath = Path.Combine(_rawFolder, SelectedLogFile);
            if (!File.Exists(filePath)) return;

            try
            {
                var processor = new CsvFileProcessor(filePath);
                int count = processor.GetRowCount();
                double durationSec = (count - 1) / 50.0; // Assuming 50Hz frequency
                if (durationSec <= 0) durationSec = 10.0;

                TrimStartSec = 0;
                TrimEndSec = durationSec;
                TrimMaxSec = durationSec;

                UpdateTrimmerPreview();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing trimmer: {ex.Message}");
            }
        }

        private void UpdateTrimmerPreview()
        {
            if (string.IsNullOrEmpty(SelectedLogFile)) return;
            string videoPath = Path.Combine(_baseFolder, "rawVideos", Path.ChangeExtension(SelectedLogFile, ".mp4"));
            if (!File.Exists(videoPath))
            {
                TrimVideoPreview = null;
                return;
            }

            int timeMs = (int)(TrimStartSec * 1000.0);
            var frame = _cameraService.GetFrameAt(videoPath, timeMs);
            if (frame != null)
            {
                TrimVideoPreview = frame;
            }
            else
            {
                TrimVideoPreview = null;
            }
        }

        public void TrimSelectedRun()
        {
            StopPlayback();
            if (string.IsNullOrEmpty(SelectedLogFile))
            {
                MessageBox.Show("Please select a log file first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string csvSrc = Path.Combine(_rawFolder, SelectedLogFile);
            string videoSrc = Path.Combine(_baseFolder, "rawVideos", Path.ChangeExtension(SelectedLogFile, ".mp4"));

            if (!File.Exists(csvSrc)) return;

            try
            {
                // Trim CSV
                var processor = new CsvFileProcessor(csvSrc);
                int startTimeMs = (int)(TrimStartSec * 1000.0);
                int endTimeMs = (int)(TrimEndSec * 1000.0);
                processor.TrimData(startTimeMs, endTimeMs);

                string csvDest = Path.Combine(_trimmedFolder, "trimmed_" + SelectedLogFile);
                processor.Save(csvDest);

                // Trim Video if it exists
                if (File.Exists(videoSrc))
                {
                    string videoDestDir = Path.Combine(_baseFolder, "trimmedVideos");
                    if (!Directory.Exists(videoDestDir)) Directory.CreateDirectory(videoDestDir);
                    string videoDest = Path.Combine(videoDestDir, "trimmed_" + Path.ChangeExtension(SelectedLogFile, ".mp4"));

                    CameraService.TrimVideo(videoSrc, videoDest, startTimeMs, endTimeMs);
                }

                MessageBox.Show($"Successfully trimmed data from {TrimStartSec:F2}s to {TrimEndSec:F2}s.\nSaved files to trimmed folders.", 
                    "Trimming Succeeded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error trimming run: {ex.Message}", "Error Trimming", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Cleanup()
        {
            StopPlayback();
            _cameraService.Dispose();
            _mediaPipeClientService.Dispose();
            _ttsSpeechService.Dispose();
        }

        private void MediaPipeClientService_HandLandmarksReceived(bool detected, List<double[]> landmarks)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                HandLandmarkPoints.Clear();
                if (detected && landmarks != null)
                {
                    foreach (var lm in landmarks)
                    {
                        if (lm.Length >= 2)
                        {
                            // Multiply by 100 for the Viewbox Canvas scaling (0-100 coordinates)
                            HandLandmarkPoints.Add(new System.Windows.Point(lm[0] * 100.0, lm[1] * 100.0));
                        }
                    }
                }
            });
        }

        private void PerformLiveGestureMatching()
        {
            if (IsRecording || _gestureCatalogService.Templates.Count == 0) return;
            if (AacSettings != null && !AacSettings.SensorInputActive) return;

            var match = _gestureCatalogService.MatchLiveGesture(_sensorRollingBuffer);
            if (match != null)
            {
                DetectedGestureName = match.Name;
                StatusText = $"Recognized Gesture: {match.Name}";
                System.Diagnostics.Debug.WriteLine($"Gesture Matched: {match.Name}");

                // Trigger gesture AAC action
                TriggerGestureAction(match.Name);
            }
            else
            {
                DetectedGestureName = "None";
            }
        }

        public void RecordGestureTemplate()
        {
            if (string.IsNullOrEmpty(NewGestureName))
            {
                MessageBox.Show("Please enter a name for the new gesture template.", "Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(SelectedLogFile))
            {
                MessageBox.Show("Please select a recorded CSV run first.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string csvPath = Path.Combine(_rawFolder, SelectedLogFile);
            if (!File.Exists(csvPath)) return;

            try
            {
                var processor = new CsvFileProcessor(csvPath);
                int startMs = (int)(TrimStartSec * 1000.0);
                int endMs = (int)(TrimEndSec * 1000.0);

                var slicedPoints = processor.GetSlicedDataPoints(startMs, endMs);
                if (slicedPoints.Count < 5)
                {
                    MessageBox.Show("Selected trimmer window is too short. Please select a longer segment.", "Duration Too Short", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _gestureCatalogService.SaveTemplate(NewGestureName, slicedPoints, NewGestureThreshold, NewGestureUseGyro);
                NewGestureName = "";
                RefreshGestureTemplates();

                MessageBox.Show($"Successfully saved gesture template from selected trimmer timeline.", 
                    "Gesture Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error recording gesture template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void DeleteTemplate(GestureTemplate template)
        {
            if (template == null) return;
            var result = MessageBox.Show($"Are you sure you want to delete gesture template '{template.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _gestureCatalogService.DeleteTemplate(template.Name);
                RefreshGestureTemplates();
            }
        }

        public void RefreshGestureTemplates()
        {
            GestureTemplates.Clear();
            _gestureCatalogService.LoadAllTemplates();
            foreach (var template in _gestureCatalogService.Templates)
            {
                GestureTemplates.Add(template);
            }
        }

        // Properties for Phase 3 binding
        public ObservableCollection<System.Windows.Point> HandLandmarkPoints
        {
            get => _handLandmarkPoints;
            set => SetField(ref _handLandmarkPoints, value);
        }

        public ObservableCollection<GestureTemplate> GestureTemplates
        {
            get => _gestureTemplates;
            set => SetField(ref _gestureTemplates, value);
        }

        public bool ImuGestureOnlyMode
        {
            get => _imuGestureOnlyMode;
            set
            {
                if (SetField(ref _imuGestureOnlyMode, value))
                {
                    if (value)
                    {
                        // Stop camera preview and MediaPipe client
                        _cameraService.StopPreview();
                        _mediaPipeClientService.Stop();
                        LiveCameraPreview = null;
                        HandLandmarkPoints.Clear();
                        StatusText = "IMU Gesture-Only Mode active (webcam offline).";
                    }
                    else
                    {
                        // Restart camera preview and MediaPipe client
                        _cameraService.StartPreview(0);
                        _mediaPipeClientService.Start(0);
                        StatusText = "Live camera preview active.";
                    }
                }
            }
        }

        public string DetectedGestureName
        {
            get => _detectedGestureName;
            set => SetField(ref _detectedGestureName, value);
        }

        public string NewGestureName
        {
            get => _newGestureName;
            set => SetField(ref _newGestureName, value);
        }

        public double NewGestureThreshold
        {
            get => _newGestureThreshold;
            set => SetField(ref _newGestureThreshold, value);
        }

        public bool NewGestureUseGyro
        {
            get => _newGestureUseGyro;
            set => SetField(ref _newGestureUseGyro, value);
        }

        // AAC Properties and Commands
        public event EventHandler<WordSpokenEventArgs>? WordSpoken;

        public ObservableCollection<VocabularyCard> AllCards { get; set; } = new ObservableCollection<VocabularyCard>();
        public ObservableCollection<ActivityModel> AllActivities { get; set; } = new ObservableCollection<ActivityModel>();
        public ObservableCollection<PronunciationItem> PronunciationDictionary { get; set; } = new ObservableCollection<PronunciationItem>();

        public AacSettings AacSettings
        {
            get => _aacSettings;
            set => SetField(ref _aacSettings, value);
        }

        public string SpeechDisplayText
        {
            get => _speechDisplayText;
            set => SetField(ref _speechDisplayText, value);
        }

        public ActivityModel? ActiveActivity
        {
            get => _activeActivity;
            set
            {
                if (SetField(ref _activeActivity, value))
                {
                    UpdateActiveCards();
                }
            }
        }

        public ObservableCollection<VocabularyCard> ActiveCards
        {
            get => _activeCards;
            set => SetField(ref _activeCards, value);
        }

        public List<string> AvailableVoices
        {
            get => _availableVoices;
            set => SetField(ref _availableVoices, value);
        }

        public bool IsSettingsVisible
        {
            get => _isSettingsVisible;
            set => SetField(ref _isSettingsVisible, value);
        }

        public int SettingsTabIdx
        {
            get => _settingsTabIdx;
            set => SetField(ref _settingsTabIdx, value);
        }

        public ObservableCollection<HeatmapCell> AccuracyMatrix
        {
            get => _accuracyMatrix;
            set => SetField(ref _accuracyMatrix, value);
        }

        // CRUD Properties for settings editing
        public string NewCardText { get => _newCardText; set => SetField(ref _newCardText, value); }
        public string NewCardAudio { get => _newCardAudio; set => SetField(ref _newCardAudio, value); }
        public string NewCardIcon { get => _newCardIcon; set => SetField(ref _newCardIcon, value); }
        public string NewCardGesture { get => _newCardGesture; set => SetField(ref _newCardGesture, value); }

        public VocabularyCard? SelectedCardForEdit
        {
            get => _selectedCardForEdit;
            set
            {
                if (SetField(ref _selectedCardForEdit, value))
                {
                    if (value != null)
                    {
                        NewCardText = value.TextDisplay;
                        NewCardAudio = value.AudioSpeechOutput;
                        NewCardIcon = value.ImagePath;
                        NewCardGesture = value.AssociatedGestureName;
                    }
                    else
                    {
                        NewCardText = "";
                        NewCardAudio = "";
                        NewCardIcon = "";
                        NewCardGesture = "";
                    }
                }
            }
        }

        public string NewActivityName { get => _newActivityName; set => SetField(ref _newActivityName, value); }
        public string NewDictOriginal { get => _newDictOriginal; set => SetField(ref _newDictOriginal, value); }
        public string NewDictPhonetic { get => _newDictPhonetic; set => SetField(ref _newDictPhonetic, value); }

        // Speech actions
        public void TriggerSpeak()
        {
            if (string.IsNullOrWhiteSpace(SpeechDisplayText)) return;
            _ttsSpeechService.SpeakAsync(SpeechDisplayText, AacSettings, PronunciationDictionary.ToList());
        }

        public void TriggerClear()
        {
            SpeechDisplayText = string.Empty;
            _ttsSpeechService.Stop();
        }

        public void ToggleSpeechOn()
        {
            AacSettings.SpeechOn = !AacSettings.SpeechOn;
            OnPropertyChanged(nameof(AacSettings));
            _aacStorageService.SaveSettings(AacSettings);
        }

        public void ToggleSensorInput()
        {
            AacSettings.SensorInputActive = !AacSettings.SensorInputActive;
            OnPropertyChanged(nameof(AacSettings));
            _aacStorageService.SaveSettings(AacSettings);
        }

        public void ToggleFullScreen()
        {
            AacSettings.FullScreenMode = !AacSettings.FullScreenMode;
            OnPropertyChanged(nameof(AacSettings));
            _aacStorageService.SaveSettings(AacSettings);
        }

        public void OpenSettings()
        {
            IsSettingsVisible = true;
            SelectedCardForEdit = null;
            CalculateAccuracyHeatmap();
        }

        public void CloseSettings()
        {
            IsSettingsVisible = false;
            SaveAacConfiguration();
        }

        public void SaveAacConfiguration()
        {
            _aacStorageService.SaveSettings(AacSettings);
            _aacStorageService.SaveCards(AllCards.ToList());
            _aacStorageService.SaveActivities(AllActivities.ToList());
            _aacStorageService.SaveDictionary(PronunciationDictionary.ToList());
        }

        private void UpdateActiveCards()
        {
            ActiveCards.Clear();
            if (ActiveActivity != null)
            {
                foreach (var id in ActiveActivity.CardIds)
                {
                    var card = AllCards.FirstOrDefault(c => c.Id == id);
                    if (card != null)
                    {
                        ActiveCards.Add(card);
                    }
                }
            }
        }

        public void TriggerGestureAction(string gestureName)
        {
            if (ImuGestureOnlyMode)
            {
                if (gestureName == AacSettings.SpeakGestureName)
                {
                    TriggerSpeak();
                }
                else if (gestureName == AacSettings.ClearGestureName)
                {
                    TriggerClear();
                }
                else
                {
                    var card = AllCards.FirstOrDefault(c => c.AssociatedGestureName == gestureName);
                    if (card != null && AacSettings.SpeechOn)
                    {
                        SpeakCard(card);
                    }
                }
            }
            else
            {
                var card = AllCards.FirstOrDefault(c => c.AssociatedGestureName == gestureName);
                if (card != null)
                {
                    // Append text
                    AppendTextFromGesture(card.TextDisplay);
                    if (AacSettings.SpeechOn)
                    {
                        SpeakCard(card);
                    }
                }
            }
        }

        public void AppendTextFromGesture(string textToAppend)
        {
            if (string.IsNullOrEmpty(textToAppend)) return;

            string current = SpeechDisplayText;
            if (!string.IsNullOrEmpty(current) && !current.EndsWith(" ") && !textToAppend.StartsWith(" ") && !char.IsPunctuation(textToAppend[0]))
            {
                current += " ";
            }
            SpeechDisplayText = current + textToAppend;

            // Trigger Speak Setting logic (word-by-word or sentence completed)
            if (AacSettings.SpeechOn)
            {
                if (AacSettings.SpeakSetting == 0) // Speak word by word
                {
                    // Already spoken via SpeakCard or we can speak it again
                }
                else if (AacSettings.SpeakSetting == 1) // Speak sentence
                {
                    if (textToAppend.EndsWith(".") || textToAppend.EndsWith("?") || textToAppend.EndsWith("!"))
                    {
                        string lastSentence = GetLastSentence(SpeechDisplayText);
                        if (!string.IsNullOrEmpty(lastSentence))
                        {
                            _ttsSpeechService.SpeakAsync(lastSentence, AacSettings, PronunciationDictionary.ToList());
                        }
                    }
                }
            }
        }

        public void SpeakCardExplicit(VocabularyCard card)
        {
            SpeakCard(card);
        }

        private void SpeakCard(VocabularyCard card)
        {
            if (!string.IsNullOrEmpty(card.AudioSpeechOutput) && 
                (card.AudioSpeechOutput.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || 
                 card.AudioSpeechOutput.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)))
            {
                if (File.Exists(card.AudioSpeechOutput))
                {
                    try
                    {
                        _mediaPlayer.Open(new Uri(card.AudioSpeechOutput));
                        _mediaPlayer.Play();
                        return;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error playing card audio file: {ex.Message}");
                    }
                }
            }

            string textToSpeak = string.IsNullOrEmpty(card.AudioSpeechOutput) ? card.TextDisplay : card.AudioSpeechOutput;
            _ttsSpeechService.SpeakAsync(textToSpeak, AacSettings, PronunciationDictionary.ToList());
        }

        private string GetLastSentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var sentences = text.Split(new[] { '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);
            if (sentences.Length == 0) return string.Empty;
            return sentences[^1].Trim();
        }

        // CRUD methods for settings panel
        public void AddNewCard()
        {
            if (string.IsNullOrWhiteSpace(NewCardText)) return;
            var newCard = new VocabularyCard
            {
                TextDisplay = NewCardText.Trim(),
                AudioSpeechOutput = NewCardAudio.Trim(),
                ImagePath = string.IsNullOrWhiteSpace(NewCardIcon) ? "❓" : NewCardIcon.Trim(),
                AssociatedGestureName = NewCardGesture
            };
            AllCards.Add(newCard);
            
            // Add to active activity if selected
            if (ActiveActivity != null)
            {
                ActiveActivity.CardIds.Add(newCard.Id);
                UpdateActiveCards();
            }

            NewCardText = "";
            NewCardAudio = "";
            NewCardIcon = "";
            NewCardGesture = "";

            SaveAacConfiguration();
        }

        public void UpdateSelectedCard()
        {
            if (SelectedCardForEdit == null || string.IsNullOrWhiteSpace(NewCardText)) return;

            SelectedCardForEdit.TextDisplay = NewCardText.Trim();
            SelectedCardForEdit.AudioSpeechOutput = NewCardAudio.Trim();
            SelectedCardForEdit.ImagePath = string.IsNullOrWhiteSpace(NewCardIcon) ? "❓" : NewCardIcon.Trim();
            SelectedCardForEdit.AssociatedGestureName = NewCardGesture;

            // Trigger list update
            int idx = AllCards.IndexOf(SelectedCardForEdit);
            if (idx >= 0)
            {
                AllCards[idx] = SelectedCardForEdit;
            }
            UpdateActiveCards();

            SelectedCardForEdit = null;
            SaveAacConfiguration();
        }

        public void DeleteCard(VocabularyCard card)
        {
            if (card == null) return;
            AllCards.Remove(card);

            // Remove from all activities
            foreach (var act in AllActivities)
            {
                act.CardIds.Remove(card.Id);
            }
            UpdateActiveCards();
            SaveAacConfiguration();
        }

        public void AddNewActivity()
        {
            if (string.IsNullOrWhiteSpace(NewActivityName)) return;
            var newAct = new ActivityModel { Name = NewActivityName.Trim() };
            AllActivities.Add(newAct);
            ActiveActivity = newAct;
            NewActivityName = "";
            SaveAacConfiguration();
        }

        public void DeleteActivity(ActivityModel activity)
        {
            if (activity == null) return;
            AllActivities.Remove(activity);
            if (ActiveActivity == activity)
            {
                ActiveActivity = AllActivities.FirstOrDefault();
            }
            SaveAacConfiguration();
        }

        public void ToggleCardInActivity(VocabularyCard card, ActivityModel activity)
        {
            if (card == null || activity == null) return;
            if (activity.CardIds.Contains(card.Id))
            {
                activity.CardIds.Remove(card.Id);
            }
            else
            {
                activity.CardIds.Add(card.Id);
            }
            if (ActiveActivity == activity)
            {
                UpdateActiveCards();
            }
            SaveAacConfiguration();
        }

        public void AddPronunciationWord()
        {
            if (string.IsNullOrWhiteSpace(NewDictOriginal) || string.IsNullOrWhiteSpace(NewDictPhonetic)) return;
            var item = new PronunciationItem
            {
                OriginalWord = NewDictOriginal.Trim(),
                PhoneticSpeech = NewDictPhonetic.Trim()
            };
            PronunciationDictionary.Add(item);
            NewDictOriginal = "";
            NewDictPhonetic = "";
            SaveAacConfiguration();
        }

        public void DeletePronunciationWord(PronunciationItem item)
        {
            if (item == null) return;
            PronunciationDictionary.Remove(item);
            SaveAacConfiguration();
        }

        public void CalculateAccuracyHeatmap()
        {
            AccuracyMatrix.Clear();
            var templates = _gestureCatalogService.Templates;
            if (templates.Count == 0) return;

            foreach (var rowTemp in templates)
            {
                foreach (var colTemp in templates)
                {
                    double dist = double.MaxValue;
                    if (rowTemp.Name.Equals(colTemp.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        dist = 0.0;
                    }
                    else if (rowTemp.Points.Count >= 5 && colTemp.Points.Count >= 5)
                    {
                        bool useGyro = rowTemp.UseGyro || colTemp.UseGyro;
                        var rFiltered = _gestureCatalogService.FilterDimensions(rowTemp.Points, useGyro);
                        var cFiltered = _gestureCatalogService.FilterDimensions(colTemp.Points, useGyro);
                        dist = DtwMatcher.ComputeDtwDistance(rFiltered, cFiltered);
                    }

                    string color = GetCellColor(dist, rowTemp.Threshold);
                    AccuracyMatrix.Add(new HeatmapCell
                    {
                        RowGesture = rowTemp.Name,
                        ColGesture = colTemp.Name,
                        Distance = dist,
                        CellColor = color
                    });
                }
            }
        }

        private string GetCellColor(double dist, double threshold)
        {
            if (dist == 0) return "#2ECC71"; // Perfect green
            if (dist == double.MaxValue || dist < 0) return "#313244"; // Gray (N/A)
            if (dist < threshold)
            {
                return "#F38BA8"; // Red/Conflict (too similar!)
            }
            else if (dist < threshold * 2.0)
            {
                return "#F9E2AF"; // Warning (moderate similarity)
            }
            else
            {
                return "#A6E3A1"; // Safe / distinct (Green)
            }
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
