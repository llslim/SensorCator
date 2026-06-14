using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace GestureFlowWPF.Services
{
    public class CameraService : IDisposable
    {
        private VideoCapture? _capture;
        private VideoWriter? _writer;
        private Thread? _cameraThread;
        private CancellationTokenSource? _cts;
        private bool _isRecording;
        private string _videoPath = string.Empty;
        private readonly object _lock = new object();

        // Dimensions
        private const int FrameWidth = 640;
        private const int FrameHeight = 480;
        private const int FPS = 30;

        public event Action<WriteableBitmap>? PreviewFrameAvailable;
        public event Action<string>? StatusChanged;

        private WriteableBitmap? _previewBitmap;

        public bool IsPreviewRunning => _cameraThread != null;

        public void StartPreview(int deviceIndex = 0)
        {
            if (IsPreviewRunning) return;

            // Quick pre-flight: check if any video capture device exists via WMI.
            // If no camera is present, don't start OpenCV at all — avoids native crashes.
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE (PNPClass = 'Image' OR PNPClass = 'Camera')");
                var devices = searcher.Get();
                if (devices.Count == 0)
                {
                    StatusChanged?.Invoke("No camera detected — camera preview skipped.");
                    return;
                }
            }
            catch
            {
                // If WMI fails, continue and let OpenCV handle it
            }

            _cts = new CancellationTokenSource();
            _cameraThread = new Thread(() => CameraLoop(deviceIndex, _cts.Token))
            {
                IsBackground = true,
                Name = "CameraCaptureThread"
            };
            _cameraThread.Start();
        }

        public void StopPreview()
        {
            if (!IsPreviewRunning) return;

            _cts?.Cancel();
            _cameraThread?.Join(2000);
            _cameraThread = null;
            _cts?.Dispose();
            _cts = null;

            lock (_lock)
            {
                StopRecordingInternal();
                _capture?.Release();
                _capture?.Dispose();
                _capture = null;
            }
        }

        public string StartRecording(string folderPath)
        {
            lock (_lock)
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
                string fileName = $"{timestamp}.mp4";
                _videoPath = Path.Combine(folderPath, fileName);

                _isRecording = true;
                return _videoPath;
            }
        }

        public void StopRecording()
        {
            lock (_lock)
            {
                StopRecordingInternal();
            }
        }

        public static void TrimVideo(string srcPath, string destPath, int startTimeMs, int endTimeMs)
        {
            if (!File.Exists(srcPath)) return;

            using var capture = new VideoCapture(srcPath);
            if (!capture.IsOpened()) return;

            double fps = capture.Fps;
            if (fps <= 0) fps = 30.0;

            int startFrame = (int)(startTimeMs / 1000.0 * fps);
            int endFrame = (int)(endTimeMs / 1000.0 * fps);
            int totalFrames = capture.FrameCount;

            if (startFrame < 0) startFrame = 0;
            if (endFrame > totalFrames) endFrame = totalFrames;

            capture.PosFrames = startFrame;

            using var writer = new VideoWriter(destPath, VideoWriter.FourCC('m', 'p', '4', 'v'), fps, new OpenCvSharp.Size(capture.FrameWidth, capture.FrameHeight));

            using var frame = new Mat();
            int currentFrame = startFrame;

            while (currentFrame <= endFrame && capture.Read(frame) && !frame.Empty())
            {
                writer.Write(frame);
                currentFrame++;
            }
        }

        private VideoCapture? _playbackCapture;
        private string _playbackFilePath = string.Empty;

        public WriteableBitmap? GetFrameAt(string filePath, int timeMs)
        {
            lock (_lock)
            {
                try
                {
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return null;

                    if (_playbackCapture == null || _playbackFilePath != filePath)
                    {
                        _playbackCapture?.Release();
                        _playbackCapture?.Dispose();
                        _playbackCapture = new VideoCapture(filePath);
                        _playbackFilePath = filePath;
                    }

                    if (!_playbackCapture.IsOpened()) return null;

                    double fps = _playbackCapture.Fps;
                    if (fps <= 0) fps = 30.0;

                    int targetFrame = (int)((timeMs / 1000.0) * fps);
                    int totalFrames = _playbackCapture.FrameCount;
                    if (targetFrame < 0) targetFrame = 0;
                    if (targetFrame >= totalFrames) targetFrame = totalFrames - 1;

                    _playbackCapture.PosFrames = targetFrame;

                    using var frame = new Mat();
                    if (_playbackCapture.Read(frame) && !frame.Empty())
                    {
                        var bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null);
                        WriteableBitmapConverter.ToWriteableBitmap(frame, bitmap);
                        return bitmap;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error seeking video: {ex.Message}");
                }
                return null;
            }
        }

        public void ClosePlayback()
        {
            lock (_lock)
            {
                _playbackCapture?.Release();
                _playbackCapture?.Dispose();
                _playbackCapture = null;
                _playbackFilePath = string.Empty;
            }
        }

        private void StopRecordingInternal()
        {
            _isRecording = false;
            if (_writer != null)
            {
                _writer.Release();
                _writer.Dispose();
                _writer = null;
                StatusChanged?.Invoke("Video recording stopped.");
            }
        }

        private void CameraLoop(int deviceIndex, CancellationToken token)
        {
            try
            {
                VideoCapture? capture;
                lock (_lock)
                {
                    _capture = new VideoCapture(deviceIndex);
                    _capture.Set(VideoCaptureProperties.FrameWidth, FrameWidth);
                    _capture.Set(VideoCaptureProperties.FrameHeight, FrameHeight);
                    capture = _capture;

                    if (!capture.IsOpened())
                    {
                        StatusChanged?.Invoke("Webcam failed to open.");
                        return;
                    }
                }

                StatusChanged?.Invoke("Webcam preview active.");

                using var frame = new Mat();
                var sw = new Stopwatch();
                int consecutiveFailures = 0;
                const int MaxFailures = 5; // bail out quickly if no real camera present

                while (!token.IsCancellationRequested)
                {
                    sw.Restart();

                    Mat? frameCopy = null;

                    lock (_lock)
                    {
                        if (capture != null && capture.Read(frame) && !frame.Empty())
                        {
                            consecutiveFailures = 0;

                            if (_isRecording)
                            {
                                if (_writer == null && !string.IsNullOrEmpty(_videoPath))
                                {
                                    _writer = new VideoWriter(_videoPath, VideoWriter.FourCC('m', 'p', '4', 'v'), FPS,
                                        new OpenCvSharp.Size(frame.Width, frame.Height));
                                    StatusChanged?.Invoke("Video recording started.");
                                }
                                _writer?.Write(frame);
                            }

                            // Clone inside the lock — keeps native Mat memory safe
                            frameCopy = frame.Clone();
                        }
                    }

                    if (frameCopy == null)
                    {
                        consecutiveFailures++;
                        if (consecutiveFailures >= MaxFailures)
                        {
                            StatusChanged?.Invoke("Webcam unavailable — no camera detected.");
                            return; // exit thread cleanly; no native crash
                        }
                        Thread.Sleep(200);
                        continue;
                    }

                    // Post frame to UI thread — BeginInvoke so we never block the background thread
                    if (Application.Current == null) { frameCopy.Dispose(); break; }
                    var fc = frameCopy;
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            if (_previewBitmap == null ||
                                _previewBitmap.PixelWidth != fc.Width ||
                                _previewBitmap.PixelHeight != fc.Height)
                            {
                                _previewBitmap = new WriteableBitmap(fc.Width, fc.Height, 96, 96,
                                    System.Windows.Media.PixelFormats.Bgr24, null);
                            }
                            WriteableBitmapConverter.ToWriteableBitmap(fc, _previewBitmap);
                            PreviewFrameAvailable?.Invoke(_previewBitmap);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Bitmap conversion error: {ex.Message}");
                        }
                        finally
                        {
                            fc.Dispose();
                        }
                    });

                    // Throttle to ~30 FPS
                    int elapsed = (int)sw.ElapsedMilliseconds;
                    int delay = (1000 / FPS) - elapsed;
                    if (delay > 0) Thread.Sleep(delay);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Camera loop error: {ex.Message}");
            }
            finally
            {
                lock (_lock)
                {
                    StopRecordingInternal();
                    _capture?.Release();
                    _capture?.Dispose();
                    _capture = null;
                }
                StatusChanged?.Invoke("Webcam preview stopped.");
            }
        }

        public void Dispose()
        {
            StopPreview();
        }
    }
}
