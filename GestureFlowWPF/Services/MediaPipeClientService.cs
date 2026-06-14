using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GestureFlowWPF.Services
{
    public class MediaPipeClientService : IDisposable
    {
        private Process? _workerProcess;
        private UdpClient? _udpClient;
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private readonly int _port = 5005;

        public event Action<bool, List<double[]>>? HandLandmarksReceived;
        public event Action<string>? StatusChanged;

        public bool IsRunning => _workerProcess != null && !_workerProcess.HasExited;

        public void Start(int deviceIndex = 0)
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();

            // Setup UDP Client
            try
            {
                _udpClient = new UdpClient();
                // Allow reusing port in case of rapid restarts
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Loopback, _port));
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"UDP Socket error: {ex.Message}");
                return;
            }

            // Start UDP Listener Task
            _listenerTask = Task.Run(() => ListenLoop(_cts.Token));

            // Start Python Subprocess via `uv`
            try
            {
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Services", "mediapipe_worker.py");
                if (!File.Exists(scriptPath))
                {
                    StatusChanged?.Invoke("Python script not found in build directory.");
                    Stop();
                    return;
                }

                // Command to run with uv
                string args = $"run --python 3.12 --with mediapipe --with opencv-python -- python \"{scriptPath}\" --device {deviceIndex} --port {_port}";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "uv.exe",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                _workerProcess = new Process { StartInfo = startInfo };
                _workerProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"MediaPipe Worker: {e.Data}");
                        if (e.Data.Contains("STARTED"))
                        {
                            StatusChanged?.Invoke("MediaPipe hand tracking started.");
                        }
                    }
                };
                _workerProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"MediaPipe Worker Error: {e.Data}");
                    }
                };

                _workerProcess.Start();
                _workerProcess.BeginOutputReadLine();
                _workerProcess.BeginErrorReadLine();

                StatusChanged?.Invoke("Starting MediaPipe hand tracking...");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Failed to start MediaPipe: {ex.Message}");
                Stop();
            }
        }

        public void Stop()
        {
            _cts?.Cancel();

            // Terminate background process
            try
            {
                if (_workerProcess != null && !_workerProcess.HasExited)
                {
                    _workerProcess.Kill(true); // Kill entire process tree
                    _workerProcess.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error killing worker process: {ex.Message}");
            }
            _workerProcess = null;

            // Close UDP socket
            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient.Dispose();
                _udpClient = null;
            }

            _cts?.Dispose();
            _cts = null;
            _listenerTask = null;

            StatusChanged?.Invoke("MediaPipe hand tracking stopped.");
        }

        private async Task ListenLoop(CancellationToken token)
        {
            var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            while (!token.IsCancellationRequested && _udpClient != null)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(token);
                    string rawJson = Encoding.UTF8.GetString(result.Buffer);
                    
                    var packet = JsonSerializer.Deserialize<MediaPipePacket>(rawJson);
                    if (packet != null)
                    {
                        HandLandmarksReceived?.Invoke(packet.detected, packet.landmarks);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"UDP Listener Error: {ex.Message}");
                    await Task.Delay(100, token);
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private class MediaPipePacket
        {
            public bool detected { get; set; }
            public List<double[]> landmarks { get; set; } = new List<double[]>();
        }
    }
}
