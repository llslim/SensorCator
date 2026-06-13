# Product Requirements Document (PRD) - GestureFlow / SensorCator

This PRD outlines the requirements, research findings, and phased release plan for building **GestureFlow (SensorCator)**. The goal is to create an assistive hand-gesture recognition tool that integrates inertial measurement unit (IMU) telemetry and computer vision feeds into an Augmentative and Alternative Communication (AAC) dashboard.

---

## 1. Product Objective
Develop a system that recognizes hand gestures through wearable IMU sensors (Movesense) and camera feeds (MediaPipe hand tracking), preprocessing signals, and classifying them to trigger Text-to-Speech (TTS) audio phrases. The system will be built incrementally, targeting a **quick-win MVP** first and adding complexity in structured phases.

---

## 2. Technology Research & Ecosystem Analysis

We investigated the developer ecosystem for existing CLIs, APIs, MCP (Model Context Protocol) servers, libraries, and open-source work that match the app's features.

### A. Wearable Sensors & BLE APIs (Movesense)
* **Movesense Device SDK & APIs**: 
  * *Official Resources*: The [Movesense GitHub](https://github.com/movesense) and [Bitbucket](https://bitbucket.org/movesense/) contain the core C++ SDK for custom on-sensor firmware, and Mobile wrapper SDKs (MDS library) for iOS and Android BLE connection.
  * *Windows BLE API*: Native Windows Runtime (WinRT) BLE APIs (`Windows.Devices.Bluetooth`) allow direct GATT service communication on Windows desktop, removing the need for MDS wrapper middleware.
  * *Web Bluetooth API*: Supported by modern browsers (`navigator.bluetooth`), enabling web apps to query and subscribe to sensor services directly.
  * *GATT Service Mapping*:
    * Movesense Service UUID: `34834800-0000-1000-8000-00805f9b34fb`
    * Command Characteristic: `34834801-0000-1000-8000-00805f9b34fb` (used to POST/GET JSON requests).
    * Data Characteristic: `34834802-0000-1000-8000-00805f9b34fb` (receives IMU data notifications).

### B. Hand Tracking & Computer Vision
* **Google MediaPipe Hand Landmarker / Gesture Recognizer**:
  * *Repo*: [google-ai-edge/mediapipe-samples](https://github.com/google-ai-edge/mediapipe-samples).
  * *Features*: Real-time tracking of 21 3D hand landmarks from a standard camera feed. It includes pre-trained gestures (e.g., thumbs up, open palm, victory) and supports custom gesture training via **MediaPipe Model Maker** using a set of images or video files.
  * *Use Case*: Provides camera-based skeleton keypoints that can augment IMU sensor data.

### C. Signal Processing & Gesture Classification
* **Dynamic Time Warping (DTW)**:
  * *Concept*: A classic time-series matching algorithm. Unlike Longest Common Subsequence (LCS) which requires continuous signal quantization (e.g. into cluster IDs using K-Means), DTW measures similarity between two temporal sequences directly in continuous coordinate space (x, y, z), accounting for differences in speed.
  * *Libraries*: FastDTW (Java/Python), or custom lightweight implementations in C#/Java.
* **Weka ML Suite**:
  * *Concept*: A collection of machine learning algorithms for data mining. SensorCator utilizes Weka's `SimpleKMeans` clustering to quantize raw acceleration/velocity coordinates.
* **ML.NET**:
  * *Concept*: Microsoft's ML library for .NET development. Supports K-Means and regression pipelines, suitable if building a Windows desktop app.
* **TensorFlow Lite (TFLite)**:
  * *Concept*: Deploying custom Deep Learning models (like LSTMs or CNNs trained on IMU datasets) directly on-device for real-time mobile/desktop inference.

### D. AAC Dashboards & Speech
* **Cboard (Open Source AAC)**:
  * *Repo*: [cboard-org/cboard-mobile](https://github.com/cboard-org/cboard-mobile).
  * *Features*: Symbol-based communication board for speech-impaired individuals. Integrates with text-to-speech engines and operates offline.
* **Speech Synthesis APIs**:
  * *Android*: `android.speech.tts.TextToSpeech`.
  * *Windows*: `System.Speech.Synthesis` (WPF) or `Windows.Media.SpeechSynthesis` (WinRT).
  * *Web*: Web Speech API (`window.speechSynthesis`).

### E. Model Context Protocol (MCP) integrations
* **Custom Gesture MCP Server**:
  * *Concept*: Create an MCP server that acts as a bridge between the physical sensor/camera stream and an AI assistant. The server exposes tools like `get_current_gesture` or `subscribe_gestures` that allow LLMs to query or react to user physical hand gestures in real-time.

---

## 3. Phased Implementation Roadmap

To avoid high complexity at start-up, development is structured into 5 progressive phases.

```
+---------------------------+     +-------------------------------+     +--------------------------------+
| Phase 1: MVP Telemetry    | --> | Phase 2: Video & Crop Tool    | --> | Phase 3: Recognition Engine    |
| - Connect Movesense BLE   |     | - Sync local video recording  |     | - Implement KMeans + LCS / DTW  |
| - Live chart acceleration |     | - Visual timeline crop tools  |     | - Dynamic pattern matching     |
+---------------------------+     +-------------------------------+     +--------------------------------+
                                                                                        |
                                                                                        v
+---------------------------+     +-------------------------------+     +--------------------------------+
| Phase 5: Gesture MCP      | <-- | Phase 4: AAC Dashboard & TTS  | <-- | Phase 3b: MediaPipe Tracking   |
| - AI Assistant agent tool |     | - Grid button-to-speech       |     | - Hand skeletal tracking       |
| - Trigger LLM actions     |     | - Auto-speak on recognition   |     | - Custom CV gesture training   |
+---------------------------+     +-------------------------------+     +--------------------------------+
```

### Phase 1: MVP Telemetry & Logging (The Quick Win)
* **Goal**: Establish stable connection to the wearable Movesense sensor, stream raw accelerometer values, and persist them.
* **Features**:
  * BLE discovery interface filtering for `"Movesense"`.
  * Subscribe to `/Meas/IMU6` notification stream.
  * Real-time 3-axis line chart plotting raw sensor feed.
  * Save raw IMU readings to local CSV files (`Timestamp, acc_x, acc_y, acc_z`).
* **Value**: Instantly validates hardware communication, data packet formatting, and charting overhead.

### Phase 2: Synchronized Video Capture & Timeline Trimming
* **Goal**: Synchronize video recordings with sensor data logs for manual labeling.
* **Features**:
  * Capture camera frames synchronously with BLE sensor clocks.
  * Local media gallery showing recorded runs.
  * Timeline trimmer UI combining video player and data line-chart.
  * Cropping logic that slices IMU CSV rows matching the user-selected video segment.

### Phase 3: Gesture Recognition Engine & Vision Tracking
* **Goal**: Implement algorithms to classify hand movements using both IMU signals and camera inputs.
* **Features**:
  * Custom `KMeans` + `LCS` classifier or direct `DTW` matcher for continuous IMU data.
  * Google `MediaPipe Hand Landmarker` integration to track 21 3D hand keypoints.
  * Training interface to save new template gestures to a local catalog.
  * **IMU Gesture-Only Mode**: A toggleable state that bypasses the camera feed completely. This mode runs headless or in low-power mode, relying entirely on the wearable Movesense sensor's IMU data to recognize gestures when the user is away from the screen.

### Phase 4: AAC Display Screen & Text-to-Speech (TTS)
* **Goal**: Provide an accessible, custom communication grid board.
* **Features**:
  * **Speech Display Text Area**: An ongoing text window that holds the constructed message. Clicking the text area places the cursor, triggers the app's on-screen keyboard, and brings up the Activity list (card collection selector). The text area highlights each word in real-time as it is spoken by the text-to-speech engine.
  * **Control Actions**: Buttons for:
    * `"Speak"`: Triggers the TTS engine to speak the full contents of the Speech Display.
    * `"Clear Display"`: Resets the Speech Display text area to empty.
    * `"Speech On/Off"`: Toggles vocal text-to-speech audio feedback.
    * `"Sensor Input"`: Toggles telemetry ingestion, controlling whether the app actively receives and processes data from the BLE sensor.
    * `"Full Screen"`: Expands the Speech Display text area to fill the entire screen. In full-screen mode, only the `"Speak"`, `"Clear Display"`, and `"Full Screen"` (serving as a toggle back) buttons remain visible.
    * `"Settings"`: Opens a Settings Modal containing a settings list consisting of:
      * **Cards**: List of vocabulary cards where each card has a `"Properties"` button that opens a **Card Properties** sub-view defining:
        * *Text Display*: The text content displayed on the card and appended to the message.
        * *Audio Speech Output*: Selectable option to use custom TTS Pronunciation phonetic text or map a recorded audio file (e.g., `.wav`, `.mp3`). If neither is specified, it defaults to speaking the Text Display string.
        * *Image/Icon*: Visual icon/picture associated with the vocabulary word.
        * *Associated Gesture*: The IMU or camera gesture template mapped to this card.
      * **Activities**: Collections of cards grouped together (acting as custom decks).
      * **Gestures**: Configuration interface for sensor and vision gesture templates. Contains tools to:
        * *Record a gesture*: Capture raw sensor/camera streams to define a new template.
        * *Edit a gesture*: Modify labels, weights, or dynamic matching thresholds.
        * *Accuracy Heatmap*: A visual similarity matrix comparing recorded trials against template databases to diagnose model precision.
      * **BLE Bindings**: Sensor pairing, GATT service settings, and peripheral bindings.
      * **Control Button Settings**: Configurations for control buttons including:
        * *Speak Setting*: Dropdown to define the Speak button's execution mode:
          * *Speak word by word*: Synthesizes each word instantly when appended.
          * *Speak sentence*: Processes text chunking, reading sentences aloud and stopping at full stop punctuation characters (`.`, `?`, `!`).
          * *Speak display*: Reads the entire contents of the text area when explicitly triggered.
        * *Speak Button Gesture*: Dropdown list to associate an IMU or camera gesture with the Speak command.
        * *Clear Display Button Gesture*: Dropdown list to associate an IMU or camera gesture with the Clear Display command.
      * **Speech Display Setting**: Customization panel to configure background/foreground colors and font sizes for the Speech Display text area to optimize accessibility and legibility.
      * **TTS Voice Properties**: Speed rate, volume, pitch, and voice engine profiles.
      * **Pronunciation Dictionary**: Custom phonetic dictionary for adjusting how specific words are synthesized by the speech engine.
  * **Interactive Word/Phrase Cards**: Activating vocabulary cards (either by clicking them manually or matching their associated gesture via Gesture Stream Input) appends the card's text to the Speech Display.
  * **Gesture Stream Input**: Background sensor/camera gesture matches activate their associated vocabulary cards.
  * **IMU Gesture-Only Mode**: Bypasses the GUI screen when the user is away from the screen. In this mode (with Speech On), a recognized IMU gesture match either speaks the card's Audio Speech Output (custom TTS or recorded audio file) or executes its associated control button command (e.g. triggering the "Speak" command to read the accumulated message or triggering "Clear Display").

### Phase 5: GestureFlow Model Context Protocol (MCP) Server
* **Goal**: Connect physical gestures to AI agent workflows.
* **Features**:
  * Build an MCP server wrapper around the GestureFlow daemon.
  * Expose tools like `get_current_pose`, `register_hot_gesture`, and `listen_gestures`.
  * Enable AI models to run custom logic (e.g. writing code, sending messages, running commands) triggered by a physical gesture.

---

## 4. Phase 1 Technical Specification (Immediate Action)

### Functional Requirements
1. **BLE Scanner**: Scans for BLE peripherals, filtering by names containing `"Movesense"`.
2. **Telemetry Client**:
   * Connects to a Movesense GATT service.
   * Configures notification subscriptions on the Data Characteristic (`34834802-0000-1000-8000-00805f9b34fb`).
   * Decodes incoming IMU packets at ~52Hz.
3. **Telemetry Plot**: Draws a rolling line-graph representing 3-axis accelerometer values ($g$-force units).
4. **Data Logger**: Writes incoming sensor readings into a CSV file inside local storage upon clicking "Record".

### Phase 1 UI Mockup Layout
```
+-------------------------------------------------------------+
|  GestureFlow - Sensor Control Hub                          |
+-------------------------------------------------------------+
| [ Scan Devices ]                                            |
|                                                             |
| Discovered Devices:                                         |
|  - Movesense 19283742  [ Connect ]                          |
|  - Movesense 18237192  [ Connect ]                          |
|                                                             |
+-------------------------------------------------------------+
| Connection Status: Connected (Movesense 19283742)           |
| Live IMU Feed (52Hz):                                       |
|  Acc X: 0.12g | Acc Y: -0.98g | Acc Z: 0.05g                |
|                                                             |
| [ RECORDING: START ]   [ SAVE CSV LOG ]                     |
+-------------------------------------------------------------+
| Live Chart:                                                 |
|  X ---                                                      |
|  Y ----------                                               |
|  Z -                                                        |
+-------------------------------------------------------------+
```
