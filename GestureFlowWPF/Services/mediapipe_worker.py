import sys
import json
import socket
import time
import argparse
import cv2
import mediapipe as mp

def main():
    parser = argparse.ArgumentParser(description="MediaPipe Hand Landmarker UDP Worker")
    parser.add_argument("--device", type=int, default=0, help="Webcam device index")
    parser.add_argument("--port", type=int, default=5005, help="Target UDP port")
    args = parser.parse_args()

    # Initialize UDP Socket
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    server_address = ('127.0.0.1', args.port)

    # Initialize MediaPipe Hands
    mp_hands = mp.solutions.hands
    # Using low complexity and tracking confidence for max speed/low latency
    hands = mp_hands.Hands(
        static_image_mode=False,
        max_num_hands=1,
        min_detection_confidence=0.5,
        min_tracking_confidence=0.5
    )

    # Open Camera
    cap = cv2.VideoCapture(args.device)
    if not cap.isOpened():
        print(f"ERROR: Could not open camera device index {args.device}", flush=True)
        sys.exit(1)

    print("STARTED: MediaPipe hand tracking worker active.", flush=True)
    sys.stdout.flush()

    try:
        while True:
            # Check for parent shutdown signal from stdin (non-blocking style read check isn't standard, 
            # but standard sys.stdin.readline is blocking. Standard practice for subprocess: 
            # if parent process dies, stdout/stderr raises exception or EOF is hit when reading stdin)
            # In Python, we can check cap.read()
            ret, frame = cap.read()
            if not ret:
                time.sleep(0.01)
                continue

            # Flip the frame horizontally for a natural mirror-view
            frame = cv2.flip(frame, 1)

            # Convert BGR to RGB
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)

            # Process with MediaPipe
            results = hands.process(rgb_frame)

            packet = {"detected": False, "landmarks": []}

            if results.multi_hand_landmarks:
                packet["detected"] = True
                hand_landmarks = results.multi_hand_landmarks[0]
                # Extract 21 landmarks
                for lm in hand_landmarks.landmark:
                    packet["landmarks"].append([lm.x, lm.y, lm.z])

            # Send over UDP
            try:
                message = json.dumps(packet).encode('utf-8')
                sock.sendto(message, server_address)
            except Exception as e:
                # Socket errors can be ignored
                pass

            # Throttle to avoid pegging CPU (approx 30 FPS)
            time.sleep(0.03)

    except KeyboardInterrupt:
        pass
    finally:
        cap.release()
        hands.close()
        sock.close()
        print("STOPPED: MediaPipe worker terminated.", flush=True)

if __name__ == "__main__":
    main()
