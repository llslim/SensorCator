# Product Requirement Document (PRD) - SensorCator WPF (Windows)

This PRD outlines the requirements, research findings, and phased release plan for porting the **SensorCator** Android app to a **WPF (.NET 8)** desktop application targeting **Windows**.

---

## 1. Product Objective
Port the core capabilities of SensorCator (IMU data recording, video synchronization, signal preprocessing, gesture comparison, and real-time classification) to a desktop environment on Windows using C# and WPF.

---

## 2. Research Findings

### A. Bluetooth Low Energy (BLE) on Windows
* **The Solution**: Use native Windows WinRT BLE APIs (`Windows.Devices.Bluetooth` and `Windows.Devices.Bluetooth.GenericAttributeProfile`) directly in the WPF application. This allows direct, robust connection to the sensor without relying on mobile-only MDS wrappers or buggy cross-platform plugins.
* **Movesense API over BLE GATT**:
  * **Movesense Service UUID**: `34834800-0000-1000-8000-00805f9b34fb`
  * **Command characteristic**: `34834801-0000-1000-8000-00805f9b34fb` (used to send PUT/GET/SUBSCRIBE request packets).
  * **Data characteristic**: `34834802-0000-1000-8000-00805f9b34fb` (receives streaming packets like raw IMU notifications).

### B. Machine Learning & Signal Processing in C#
* **Option 1: ML.NET (Microsoft)**: Heavy-duty ML library supporting K-Means. While powerful, it introduces high assembly footprints and execution overhead (e.g. initializing `MLContext` and setting up pipeline structures), which is unnecessary for simple sequence clustering.
* **Option 2: Custom C# Algorithms (Recommended)**: Implement a lightweight, native C# class for:
  * **K-Means Clustering**: A simple, iterative centroid-assignment algorithm.
  * **Sequence Matching (LCS)**: A classic dynamic programming algorithm to calculate similarity scores.
  * **Benefit**: Zero external dependencies, extremely fast startup/inference times, and easy cross-platform porting to macOS/mobile if required in the future.

### C. Data Logging & Visualizations
* **Signal Plotting**: Use **LiveCharts2** (specifically `LiveChartsCore.SkiaSharpView.WPF`), which is open-source, highly responsive on Windows, and integrates directly with WPF.
* **Storage**: Store logged sensor runs as standard CSV files matching the schema: `Timestamp,acc_x,acc_y,acc_z,gyro_x,gyro_y,gyro_z,magn_x,magn_y,magn_z`.

---

## 3. Phased Implementation Roadmap

```
Phase 1: MVP Core Pipeline  ===>  Phase 2: Video & Trimming  ===>  Phase 3: KMeans & Recognition  ===>  Phase 4: Analytics Dashboard
(IMU Connection & Logging)       (Camera Recording & Slicing)       (Real-Time Classification)         (Similarity Heatmaps)
```

### **Phase 1: Quick Win (MVP)**
* **Goals**: Connect to a standard Movesense sensor on Windows, stream IMU telemetry, and save the data to CSV log files.
* **Scope**:
  * BLE scan page showing detected Movesense devices.
  * Establish GATT connection and subscribe to `/Meas/IMU6` at 52Hz.
  * Live telemetry log on-screen showing X, Y, Z accelerometer values.
  * Save raw IMU files to local user documents directory.
  * Basic offline visualization plotting saved CSV logs on a 3-axis line chart.
* **Value**: Validates BLE telemetry, C# data structures, file persistence, and charting dependencies without complex UI or video capture overhead.

### **Phase 2: Video Integration & Trimming**
* **Goals**: Coordinate synchronized video capture and add data cropping.
* **Scope**:
  * Implement video recording using the phone/webcam feed during IMU logging.
  * Add the **Video List Page** showing captured runs.
  * Add the **Trim View** containing a video player and slider timeline.
  * Implement the C# `CsvFileProcessor` class to trim and slice the corresponding IMU log files based on video timestamps.

### **Phase 3: Real-Time Recognition & AAC Dashboard**
* **Goals**: Integrate the C# KMeans + LCS classifier and build the speech output page.
* **Scope**:
  * Implement the custom `KMeansClassifier` and `LcsMatcher` in C#.
  * Add the **AAC Speech Grid Page** (clickable card grid with associated text).
  * Implement background streaming classification. When a gesture is recognized, trigger Windows native Text-to-Speech (`System.Speech.Synthesis`) to speak the phrase.

### **Phase 4: Advanced Visualizations & Analytics**
* **Goals**: Build tools for gesture model verification.
* **Scope**:
  * Add the **Filters Panel** to tune weights for raw Acc, smoothed Acc, and Gyro channels.
  * Add the **Heatmap Matrix** showing similarity scores across recorded gesture trials.
  * Support model saving and profile updates.

---

## 4. Phase 1 Technical Specification (Immediate Action)

### Functional Requirements
1. **Device Scanning**: Clicking "Scan" activates the Windows BLE watcher, filtering devices matching `"Movesense"`.
2. **Device Connection**: Selecting a device triggers connection, queries the Movesense custom GATT service, and subscribes to notifications.
3. **Data Stream**: Receives notifications containing IMU6 packets and prints x/y/z accelerometer values to the UI at 50-52Hz.
4. **Logging**: Clicking "Record" creates a CSV file in `Documents/SensorCator/rawData/` and streams records until stopped.
5. **Graph Viewer**: A basic page displaying a dropdown of logged CSV files. Selecting a file plots the x/y/z accelerometer telemetry on a line chart.

### Interface Mockup
* **Main Dashboard**: Simple tabs for **Connect/Record** and **View Logs**.
* **Connect/Record Tab**: A list view of discovered BLE devices, a connect button, and a prominent start/stop recording button.
* **View Logs Tab**: A file picker/dropdown displaying saved CSV files, and a Chart plotting the raw signal.
