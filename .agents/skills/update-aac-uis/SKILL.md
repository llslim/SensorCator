---
name: update-aac-uis
description: >-
  Synchronizes the user interfaces of GestureFlow, SpeakDirect, and AllyAAC with the canonical version of ScoobyUI.
  Use this skill or slash command (/update-aac-uis) whenever changes are made to ScoobyUI-AAC (such as layout updates,
  new buttons, Catppuccin theme tweaks, 180-deg Partner Screen Flip, or multi-conversation features) and need to be
  propagated to GestureFlow, SpeakDirect, and AllyAAC while preserving their application-specific hardware and broadcast capabilities.
---

# Update AAC User Interfaces (`/update-aac-uis`)

The `update-aac-uis` skill and slash command (`/update-aac-uis`) provide an automated pipeline to synchronize the canonical **ScoobyUI** design system across all sibling AAC applications in the workspace:

1. **AllyAAC** (Progressive Web App in `D:\projects\linux\aac_pwa`)
2. **GestureFlow** (WPF & Windows Desktop AAC in `D:\projects\windows\GestureFlow`)
3. **SpeakDirect** (PRC-Saltillo Accent 1000 Partner Mirroring in `D:\projects\windows\SpeakDirect`)
4. **ScoobyUI Prototype** (In-repo testing prototype in `D:\projects\windows\ScoobyUI-AAC`)

---

## Architecture & Source of Truth

```
                       +-----------------------------------+
                       |      Canonical Source of Truth    |
                       |  D:\projects\windows\ScoobyUI-AAC |
                       |           index.html              |
                       +-----------------+-----------------+
                                         |
                                         v
                       +-----------------------------------+
                       |         sync_aac_uis.js           |
                       |       Automated Transformer       |
                       +-----+-----------+-----------+-----+
                             |           |           |
             +---------------+           |           +---------------+
             v                           v                           v
+------------------------+  +------------------------+  +------------------------+
|        AllyAAC         |  |      GestureFlow       |  |      SpeakDirect       |
|      (aac_pwa)         |  |   (GestureFlowWPF)     |  |     (Desktop Kiosk)    |
|------------------------|  |------------------------|  |------------------------|
| - PWA Manifest & Icons |  | - Movesense 9-DoF Pill |  | - Partner Status Pill  |
| - Offline ServiceWorker|  | - MediaPipe 21 Hand Pill| | - QR Code Hotspot Modal|
| - Local TailwindCSS    |  | - WPF WebView2 IPC     |  | - Live WebSocket Stream|
| - Install App Prompt   |  | - Pinch-to-Speak Hook  |  | - Floating Reactions   |
+------------------------+  +------------------------+  +------------------------+
```

---

## Target-Specific Adaptations

When the synchronizer runs, it automatically preserves and injects the critical capabilities required by each target:

### 1. AllyAAC (`D:\projects\linux\aac_pwa\index.html`)
- **Branding**: `window.SCOOBY_CONFIG.appName = "AllyAAC"`, Title: `AllyAAC - Adaptive Communication PWA`.
- **100% Offline Caching**: Replaces remote CDN Tailwind with local `tailwindcss.min.js`.
- **PWA Meta & Manifest**: Links `manifest.webmanifest`, `apple-touch-icon`, and translucent theme colors.
- **PWA Install Prompt**: Injects `#pwa-install-btn` into the top bar, reacting to `beforeinstallprompt`.
- **Service Worker**: Injects automatic registration for `sw.js` (cache-first network fallback).

### 2. GestureFlow (`D:\projects\windows\GestureFlow`)
- **Files**: `gestureflow_ui.html` and `GestureFlowWPF\scoobyui.html`.
- **Branding**: `window.SCOOBY_CONFIG.appName = "GestureFlow"`, Title: `GestureFlow - Adaptive Gesture AAC (Movesense & MediaPipe)`.
- **Telemetry Pill**: Displays live Movesense 52Hz connection status and MediaPipe hand tracking status.
- **WebView2 IPC Protocol**:
  - Inbound: Listens for `GESTURE_RECOGNIZED` (appends word chip), `PINCH_DETECTED` (triggers TTS), `BLE_STATE_CHANGED`, and `VISION_STATE_CHANGED`.
  - Outbound: Dispatches `GESTUREFLOW_TEXT_CHANGED` to C# WPF host whenever message text changes.

### 3. SpeakDirect (`D:\projects\windows\SpeakDirect\speakdirect_ui.html`)
- **Branding**: `window.SCOOBY_CONFIG.appName = "SpeakDirect"`, Title: `SpeakDirect - Partner AAC Communication Bridge`.
- **Partner Pill**: Displays real-time companion connection count (`1 Partner Connected`) and QR modal trigger.
- **Companion Onboarding Modal**: Displays local Wi-Fi Direct hotspot URL and QR code for scanning by smartphones.
- **Live Text Mirroring**: Dispatches `SPEAKDIRECT_BROADCAST_TEXT` to the C# WebSocket server whenever text updates.
- **Floating Partner Reactions**: Listens for `REACTION_RECEIVED` from partner phones and displays floating animated reaction bubbles.

---

## How to Execute

### One-Command Synchronization
Run the Node synchronization script:
```powershell
node C:\Users\kevin\.gemini\config\plugins\update-aac-uis-plugin\scripts\sync_aac_uis.js
```

### Selective Synchronization
To sync only a specific project:
```powershell
# Sync AllyAAC only
node C:\Users\kevin\.gemini\config\plugins\update-aac-uis-plugin\scripts\sync_aac_uis.js --target allyaac

# Sync GestureFlow only
node C:\Users\kevin\.gemini\config\plugins\update-aac-uis-plugin\scripts\sync_aac_uis.js --target gestureflow

# Sync SpeakDirect only
node C:\Users\kevin\.gemini\config\plugins\update-aac-uis-plugin\scripts\sync_aac_uis.js --target speakdirect
```

### Dry Run / Status Check
To inspect if targets are in sync without writing files:
```powershell
node C:\Users\kevin\.gemini\config\plugins\update-aac-uis-plugin\scripts\sync_aac_uis.js --check
```

---

## Verification & Post-Sync Checklist

After running `/update-aac-uis`:
1. **AllyAAC**:
   - Verify `http://localhost:8082` opens in browser.
   - Verify 180° Partner Screen Flip button works.
   - Verify Service Worker registers without CDN errors.
2. **GestureFlow**:
   - Verify `gestureflow_ui.html` and `GestureFlowWPF\scoobyui.html` exist and contain Movesense telemetry pill.
3. **SpeakDirect**:
   - Verify `speakdirect_ui.html` exists and contains `#partner-status-badge` and QR code modal.
4. **Git Status**:
   - Stage and commit synchronized UI files in each repository.
