using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using GestureFlowWPF.Models;

namespace GestureFlowWPF.Services
{
    public class DiscoveredDevice
    {
        public string Name { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public ulong BluetoothAddress { get; set; }

        public override string ToString() => $"{Name} ({MacAddress})";
    }

    public class BluetoothService
    {
        private BluetoothLEAdvertisementWatcher? _watcher;
        private BluetoothLEDevice? _device;
        private GattCharacteristic? _writeCharacteristic;
        private GattCharacteristic? _notifyCharacteristic;
        
        public event Action<DiscoveredDevice>? DeviceDiscovered;
        public event Action<SensorDataModel>? DataReceived;
        public event Action<string>? StatusChanged;

        private readonly Guid GSP_SERVICE_UUID = new Guid("34802252-7185-4d5d-b431-630e7050e8f0");
        private readonly Guid GSP_WRITE_UUID = new Guid("34800001-7185-4d5d-b431-630e7050e8f0");
        private readonly Guid GSP_NOTIFY_UUID = new Guid("34800002-7185-4d5d-b431-630e7050e8f0");

        public bool IsScanning { get; private set; }
        public bool IsConnected => _device != null;

        public void StartScanning()
        {
            if (IsScanning) return;

            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            _watcher.Received += Watcher_Received;
            _watcher.Start();
            IsScanning = true;
            StatusChanged?.Invoke("Scanning for Movesense devices...");
        }

        public void StopScanning()
        {
            if (!IsScanning || _watcher == null) return;

            _watcher.Stop();
            _watcher.Received -= Watcher_Received;
            _watcher = null;
            IsScanning = false;
            StatusChanged?.Invoke("Scanning stopped.");
        }

        private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            string name = args.Advertisement.LocalName;
            if (string.IsNullOrEmpty(name) || !name.StartsWith("Movesense", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string mac = FormatMacAddress(args.BluetoothAddress);
            var device = new DiscoveredDevice
            {
                Name = name,
                MacAddress = mac,
                BluetoothAddress = args.BluetoothAddress
            };

            DeviceDiscovered?.Invoke(device);
        }

        private string FormatMacAddress(ulong address)
        {
            var temp = address.ToString("X12");
            var regex = new System.Text.RegularExpressions.Regex("(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})");
            return regex.Replace(temp, "$1:$2:$3:$4:$5:$6");
        }

        public async Task<bool> ConnectAsync(ulong bluetoothAddress)
        {
            StopScanning();
            StatusChanged?.Invoke($"Connecting to {FormatMacAddress(bluetoothAddress)}...");

            try
            {
                _device = await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddress);
                if (_device == null)
                {
                    StatusChanged?.Invoke("Failed to connect: Device not found.");
                    return false;
                }

                _device.ConnectionStatusChanged += Device_ConnectionStatusChanged;

                var servicesResult = await _device.GetGattServicesForUuidAsync(GSP_SERVICE_UUID, BluetoothCacheMode.Uncached);
                if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
                {
                    var allServices = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                    string serviceList = "None";
                    if (allServices.Status == GattCommunicationStatus.Success && allServices.Services.Count > 0)
                    {
                        serviceList = string.Join(", ", allServices.Services.Select(s => s.Uuid.ToString().Substring(0, 8)));
                    }
                    StatusChanged?.Invoke($"GSP Service not found. Discovered: [{serviceList}] (Status: {servicesResult.Status}).");
                    await DisconnectAsync(true);
                    return false;
                }

                var service = servicesResult.Services[0];
                var characteristicsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                if (characteristicsResult.Status != GattCommunicationStatus.Success)
                {
                    StatusChanged?.Invoke($"Failed to retrieve characteristics (Status: {characteristicsResult.Status}).");
                    await DisconnectAsync(true);
                    return false;
                }

                foreach (var characteristic in characteristicsResult.Characteristics)
                {
                    if (characteristic.Uuid == GSP_WRITE_UUID)
                    {
                        _writeCharacteristic = characteristic;
                    }
                    else if (characteristic.Uuid == GSP_NOTIFY_UUID)
                    {
                        _notifyCharacteristic = characteristic;
                    }
                }

                if (_writeCharacteristic == null || _notifyCharacteristic == null)
                {
                    StatusChanged?.Invoke("GSP characteristics not found.");
                    await DisconnectAsync(true);
                    return false;
                }

                // Subscribe to notifications
                var cccdStatus = await _notifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);

                if (cccdStatus != GattCommunicationStatus.Success)
                {
                    StatusChanged?.Invoke($"Failed to register for notifications (Status: {cccdStatus}).");
                    await DisconnectAsync(true);
                    return false;
                }

                _notifyCharacteristic.ValueChanged += NotifyCharacteristic_ValueChanged;
                StatusChanged?.Invoke("Connected successfully.");
                return true;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Connection error: {ex.Message}");
                await DisconnectAsync(true);
                return false;
            }
        }

        private void Device_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                StatusChanged?.Invoke("Device disconnected.");
                Task.Run(async () => await DisconnectAsync());
            }
        }

        public async Task<bool> SubscribeImuAsync(string path = "/Meas/IMU9/52")
        {
            if (_writeCharacteristic == null)
            {
                StatusChanged?.Invoke("Cannot subscribe: not connected.");
                return false;
            }

            try
            {
                // Subscribe command format: [0x01, ClientRef, path_bytes]
                byte[] pathBytes = Encoding.UTF8.GetBytes(path);
                byte[] payload = new byte[2 + pathBytes.Length];
                payload[0] = 0x01; // Subscribe command ID
                payload[1] = 0x01; // Client reference ID
                Array.Copy(pathBytes, 0, payload, 2, pathBytes.Length);

                var writer = new DataWriter();
                writer.WriteBytes(payload);

                var status = await _writeCharacteristic.WriteValueAsync(writer.DetachBuffer());
                if (status == GattCommunicationStatus.Success)
                {
                    StatusChanged?.Invoke($"Subscribed to {path}.");
                    return true;
                }
                else
                {
                    StatusChanged?.Invoke($"Subscription failed: {status}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Subscription error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UnsubscribeImuAsync(string path = "/Meas/IMU9/52")
        {
            if (_writeCharacteristic == null) return false;

            try
            {
                // Unsubscribe command format: [0x02, ClientRef, path_bytes]
                byte[] pathBytes = Encoding.UTF8.GetBytes(path);
                byte[] payload = new byte[2 + pathBytes.Length];
                payload[0] = 0x02; // Unsubscribe command ID
                payload[1] = 0x01; // Client reference ID
                Array.Copy(pathBytes, 0, payload, 2, pathBytes.Length);

                var writer = new DataWriter();
                writer.WriteBytes(payload);

                var status = await _writeCharacteristic.WriteValueAsync(writer.DetachBuffer());
                if (status == GattCommunicationStatus.Success)
                {
                    StatusChanged?.Invoke($"Unsubscribed from {path}.");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Unsubscribe error: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync(bool silent = false)
        {
            if (!silent) StatusChanged?.Invoke("Disconnecting...");

            if (_notifyCharacteristic != null)
            {
                try
                {
                    _notifyCharacteristic.ValueChanged -= NotifyCharacteristic_ValueChanged;
                    await _notifyCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                }
                catch { }
                _notifyCharacteristic = null;
            }

            _writeCharacteristic = null;

            if (_device != null)
            {
                _device.ConnectionStatusChanged -= Device_ConnectionStatusChanged;
                _device.Dispose();
                _device = null;
            }

            if (!silent) StatusChanged?.Invoke("Disconnected.");
        }

        private void NotifyCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            byte[] bytes = new byte[args.CharacteristicValue.Length];
            reader.ReadBytes(bytes);

            if (bytes.Length < 6) return;

            byte resultType = bytes[0];
            byte clientRef = bytes[1];

            // 0x02 indicates a data notification
            if (resultType != 0x02) return;

            uint timestamp = BitConverter.ToUInt32(bytes, 2);

            // Let's parse the notification payload.
            // We use a robust self-adapting parsing method to support both structured arrays
            // and flat interleaved float streams.
            
            int remainingBytes = bytes.Length - 6;
            
            // Check structured array format:
            // Acc size byte at index 6, followed by elements.
            if (remainingBytes >= 2)
            {
                int accLen = bytes[6];
                int expectedAccBytes = accLen * 12; // 3 floats * 4 bytes
                
                // Ensure array size byte is reasonable and check structure
                if (accLen > 0 && accLen <= 10 && remainingBytes >= 1 + expectedAccBytes + 1 + expectedAccBytes)
                {
                    int gyroIdx = 6 + 1 + expectedAccBytes;
                    int gyroLen = bytes[gyroIdx];
                    int expectedGyroBytes = gyroLen * 12;

                    if (gyroLen == accLen && remainingBytes >= 1 + expectedAccBytes + 1 + expectedGyroBytes)
                    {
                        // Check if Mag exists
                        int magIdx = gyroIdx + 1 + expectedGyroBytes;
                        bool hasMag = false;
                        int magLen = 0;
                        if (remainingBytes >= 1 + expectedAccBytes + 1 + expectedGyroBytes + 1)
                        {
                            magLen = bytes[magIdx];
                            if (magLen == accLen && remainingBytes >= 1 + expectedAccBytes + 1 + expectedGyroBytes + 1 + magLen * 12)
                            {
                                hasMag = true;
                            }
                        }

                        // Structured array format parse
                        for (int i = 0; i < accLen; i++)
                        {
                            int accOffset = 6 + 1 + i * 12;
                            int gyroOffset = gyroIdx + 1 + i * 12;
                            int magOffset = magIdx + 1 + i * 12;

                            float ax = BitConverter.ToSingle(bytes, accOffset);
                            float ay = BitConverter.ToSingle(bytes, accOffset + 4);
                            float az = BitConverter.ToSingle(bytes, accOffset + 8);

                            float gx = BitConverter.ToSingle(bytes, gyroOffset);
                            float gy = BitConverter.ToSingle(bytes, gyroOffset + 4);
                            float gz = BitConverter.ToSingle(bytes, gyroOffset + 8);

                            float mx = 0, my = 0, mz = 0;
                            if (hasMag)
                            {
                                mx = BitConverter.ToSingle(bytes, magOffset);
                                my = BitConverter.ToSingle(bytes, magOffset + 4);
                                mz = BitConverter.ToSingle(bytes, magOffset + 8);
                            }

                            // Output data model item
                            long sampleTimestamp = timestamp + (i * 20L); // ~50Hz sample time spacing (20ms)
                            var model = new SensorDataModel
                            {
                                Timestamp = sampleTimestamp,
                                AccX = ax,
                                AccY = ay,
                                AccZ = az,
                                GyroX = gx,
                                GyroY = gy,
                                GyroZ = gz,
                                MagX = mx,
                                MagY = my,
                                MagZ = mz
                            };
                            DataReceived?.Invoke(model);
                        }
                        return;
                    }
                }
            }

            // Flat interleaved format fallback parse:
            // 9 floats (IMU9) or 6 floats (IMU6) repeated.
            int numFloats = remainingBytes / 4;
            if (numFloats >= 6)
            {
                // Determine whether it's 9 floats (IMU9) or 6 floats (IMU6) based on remainder
                int stride = (numFloats % 9 == 0) ? 9 : 6;
                int numSamples = numFloats / stride;

                for (int i = 0; i < numSamples; i++)
                {
                    int offset = 6 + i * stride * 4;

                    float ax = BitConverter.ToSingle(bytes, offset);
                    float ay = BitConverter.ToSingle(bytes, offset + 4);
                    float az = BitConverter.ToSingle(bytes, offset + 8);

                    float gx = BitConverter.ToSingle(bytes, offset + 12);
                    float gy = BitConverter.ToSingle(bytes, offset + 16);
                    float gz = BitConverter.ToSingle(bytes, offset + 20);

                    float mx = 0, my = 0, mz = 0;
                    if (stride == 9)
                    {
                        mx = BitConverter.ToSingle(bytes, offset + 24);
                        my = BitConverter.ToSingle(bytes, offset + 28);
                        mz = BitConverter.ToSingle(bytes, offset + 32);
                    }

                    long sampleTimestamp = timestamp + (i * 20L); // ~50Hz sample time spacing (20ms)
                    var model = new SensorDataModel
                    {
                        Timestamp = sampleTimestamp,
                        AccX = ax,
                        AccY = ay,
                        AccZ = az,
                        GyroX = gx,
                        GyroY = gy,
                        GyroZ = gz,
                        MagX = mx,
                        MagY = my,
                        MagZ = mz
                    };
                    DataReceived?.Invoke(model);
                }
            }
        }
    }
}
