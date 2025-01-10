package com.tanujn45.a11y;

import android.Manifest;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.os.Environment;
import android.util.Log;
import android.view.View;
import android.widget.ImageButton;
import android.widget.TextView;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.camera.core.Camera;
import androidx.camera.core.CameraSelector;
import androidx.camera.core.Preview;
import androidx.camera.lifecycle.ProcessCameraProvider;
import androidx.camera.video.FileOutputOptions;
import androidx.camera.video.Quality;
import androidx.camera.video.QualitySelector;
import androidx.camera.video.Recorder;
import androidx.camera.video.Recording;
import androidx.camera.video.VideoCapture;
import androidx.camera.video.VideoRecordEvent;
import androidx.camera.view.PreviewView;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;

import com.google.common.util.concurrent.ListenableFuture;
import com.google.gson.Gson;
import com.movesense.mds.Mds;
import com.movesense.mds.MdsException;
import com.movesense.mds.MdsHeader;
import com.movesense.mds.MdsNotificationListener;
import com.movesense.mds.MdsResponseListener;
import com.movesense.mds.MdsSubscription;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.OutputStreamWriter;
import java.text.MessageFormat;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;

public class RecordActivity extends AppCompatActivity {
    private static final String LOG_TAG = RecordActivity.class.getSimpleName();
    public static final String URI_EVENTLISTENER = "suunto://MDS/EventListener";
    private static final String URI_TIME = "suunto://{0}/Time";
    private static final String PATH = "/Meas/IMU9/";
    private static final String RATE = "52";
    private static final String RECORDING = "Recording";
    private static final String RECORD = "Record";
    public static final String FILE_TYPE = ".csv";
    private String fileNameSave;
    private MdsSubscription mdsSubscription;
    private boolean isRecording = false;
    private File directory;
    FileOutputStream fos;
    OutputStreamWriter writer;
    ImageButton recordButton;
    TextView sensorMsg;
    ExecutorService service;
    Recording recording = null;
    VideoCapture<Recorder> videoCapture = null;
    PreviewView previewView;
    int cameraFacing = CameraSelector.LENS_FACING_BACK;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_record);

        // UI elements
        sensorMsg = findViewById(R.id.sensorMsg);
        recordButton = findViewById(R.id.recordWholeButton);
        previewView = findViewById(R.id.previewView);

        // Get the connected serial
        String connectedSerial = getConnectedSerial();
        if (connectedSerial == null || BluetoothActivity.mMds == null) {
            Toast.makeText(this, "No device connected", Toast.LENGTH_SHORT).show();
//            Intent intent = new Intent(RecordActivity.this, MainActivity.class);
//            startActivity(intent);
            finish();
            return;
        }
        setCurrentTimeToSensor(connectedSerial);
        subscribeToSensor(connectedSerial);

        // Set the directory to save the file
        directory = getExternalFilesDir(Environment.DIRECTORY_DOCUMENTS);

        // Create the directories to save the raw data and raw videos
        createDirectories();

        // Start the camera
        startCamera(cameraFacing);
    }

    /**
     * Create the directories to save the raw data and raw videos
     */
    public void createDirectories() {
        File rawData = new File(directory, "rawData");
        File trimmedData = new File(directory, "trimmedData");
        File models = new File(directory, "models");
        File rawVideos = new File(directory, "rawVideos");

        if (!rawData.exists()) {
            rawData.mkdirs();
        }

        if (!trimmedData.exists()) {
            trimmedData.mkdirs();
        }

        if (!models.exists()) {
            models.mkdirs();
        }

        if (!rawVideos.exists()) {
            rawVideos.mkdirs();
        }
    }

    /**
     * Capture the video
     * If the video is already being captured, stop the video
     * If the video is not being captured, start the video
     */
    public void captureVideo() {
        Recording recording1 = recording;

        if (recording1 != null) {
            recording1.stop();
            recording = null;
            return;
        }

        String name = new SimpleDateFormat("yyyy_MM_dd_HH_mm_ss", Locale.getDefault()).format(System.currentTimeMillis()) + ".mp4";

//        ContentValues contentValues = new ContentValues();
//        contentValues.put(MediaStore.MediaColumns.DISPLAY_NAME, name);
//        contentValues.put(MediaStore.MediaColumns.MIME_TYPE, "video/mp4");
//        contentValues.put(MediaStore.Video.Media.RELATIVE_PATH, "Movies/CameraX-Video");
//
//        MediaStoreOutputOptions options = new MediaStoreOutputOptions.Builder(getContentResolver(), MediaStore.Video.Media.EXTERNAL_CONTENT_URI).setContentValues(contentValues).build();

        File file = new File(directory + "/rawVideos/", name);

        FileOutputOptions options = new FileOutputOptions.Builder(file).build();


        if (ActivityCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
            String msg = "Record audio permission not granted";
            Toast.makeText(this, msg, Toast.LENGTH_SHORT).show();
            return;
        }
        recording = videoCapture.getOutput().prepareRecording(RecordActivity.this, options).withAudioEnabled().start(ContextCompat.getMainExecutor(RecordActivity.this), videoRecordEvent -> {
            if (videoRecordEvent instanceof VideoRecordEvent.Finalize) {
                if (((VideoRecordEvent.Finalize) videoRecordEvent).hasError()) {
                    recording.close();
                    recording = null;
                    String msg = "Error: " + ((VideoRecordEvent.Finalize) videoRecordEvent).getError();
                    Toast.makeText(this, msg, Toast.LENGTH_SHORT).show();
                }
            }
        });
    }


    /**
     * Start the camera
     *
     * @param cameraFacing: The camera facing to start the camera
     */
    public void startCamera(int cameraFacing) {
        ListenableFuture<ProcessCameraProvider> processCameraProvider = ProcessCameraProvider.getInstance(RecordActivity.this);

        processCameraProvider.addListener(() -> {
            try {
                ProcessCameraProvider cameraProvider = processCameraProvider.get();
                Preview preview = new Preview.Builder().build();
                preview.setSurfaceProvider(previewView.getSurfaceProvider());

                Recorder recorder = new Recorder.Builder().setQualitySelector(QualitySelector.from(Quality.HIGHEST)).build();
                videoCapture = VideoCapture.withOutput(recorder);

                cameraProvider.unbindAll();

                CameraSelector cameraSelector = new CameraSelector.Builder().requireLensFacing(cameraFacing).build();

                Camera camera = cameraProvider.bindToLifecycle(this, cameraSelector, preview, videoCapture);


            } catch (ExecutionException | InterruptedException e) {
                e.printStackTrace();
            }
        }, ContextCompat.getMainExecutor(RecordActivity.this));
    }


    /**
     * Get the Mds instance from MainActivity
     *
     * @return Mds
     */
    private Mds getMds() {
        if (MainActivity.mMds == null) {
            return BluetoothActivity.mMds;
        }
        return MainActivity.mMds;
    }


    /**
     * Get the connected serial from MainActivity
     *
     * @return String
     */
    private String getConnectedSerial() {
        if (MainActivity.connectedSerial == null) {
            return BluetoothActivity.connectedSerial;
        }
        return MainActivity.connectedSerial;
    }


    /**
     * Record the gesture data
     * If start recording, create a new file and write the data to it
     * If stop recording, close the file and show a toast message
     *
     * @param view: The view that was clicked
     * @throws IOException : If the file cannot be written to
     */
    public void recordGestureButtonClicked(View view) throws IOException {
        isRecording = !isRecording;

        if (isRecording) {

            captureVideo();
            recordButton.setImageResource(R.drawable.recording);

            SimpleDateFormat sdf = new SimpleDateFormat("yyyy_MM_dd_HH_mm_ss", Locale.getDefault());
            String currentDateTime = sdf.format(new Date());
            fileNameSave = currentDateTime + FILE_TYPE;

            File file = new File(directory + "/rawData/", fileNameSave);
            fos = new FileOutputStream(file);

            writer = new OutputStreamWriter(fos);
            writer.append("Timestamp,acc_x,acc_y,acc_z,gyro_x,gyro_y,gyro_z,magn_x,magn_y,magn_z").append("\n");
        } else {
            captureVideo();
            writer.flush();
            writer.close();
            fos.close();

            // Toast.makeText(RecordActivity.this, "File saved as " + fileNameSave, Toast.LENGTH_SHORT).show();

            recordButton.setImageResource(R.drawable.record);
        }
    }

    public void turnCameraButtonClicked(View view) {
        if (cameraFacing == CameraSelector.LENS_FACING_BACK) {
            cameraFacing = CameraSelector.LENS_FACING_FRONT;
        } else {
            cameraFacing = CameraSelector.LENS_FACING_BACK;
        }
        startCamera(cameraFacing);
    }


    /**
     * Subscribe to the sensor data
     * If the subscription is already active, unsubscribe
     * Create a new subscription to the sensor data
     *
     * @param connectedSerial: The serial number of the connected device
     */
    private void subscribeToSensor(String connectedSerial) {
        if (mdsSubscription != null) {
            unsubscribe();
        }

        String strContract = "{\"Uri\": \"" + connectedSerial + PATH + RATE + "\"}";

        mdsSubscription = getMds().builder().build(this).subscribe(URI_EVENTLISTENER, strContract, new MdsNotificationListener() {
            @Override
            public void onNotification(String data) {
                ImuModel imuModel = new Gson().fromJson(data, ImuModel.class);
                if (imuModel == null) {
                    return;
                }
                ImuModel.Body body = imuModel.getBody();
                if (body == null) {
                    return;
                }
                if (body.getArrayAcc() == null || body.getArrayGyro() == null) {
                    return;
                }
                if (body.getArrayAcc().length == 0 || body.getArrayGyro().length == 0) {
                    return;
                }

                ImuModel.ArrayAcc[] arrayAcc = body.getArrayAcc();
                ImuModel.ArrayGyro[] arrayGyro = body.getArrayGyro();
                ImuModel.ArrayMag[] arrayMag = body.getArrayMag();
                long timestamp = imuModel.getBody().getTimestamp();

                for (int i = 0; i < arrayAcc.length; i++) {
                    String resultStrRecord = String.format(Locale.getDefault(), "%d,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f", (timestamp + i * 20L), arrayAcc[i].getX(), arrayAcc[i].getY(), arrayAcc[i].getZ(), arrayGyro[i].getX(), arrayGyro[i].getY(), arrayGyro[i].getZ(), arrayMag[i].getX(), arrayMag[i].getY(), arrayMag[i].getZ());
                    String sensorMsgStr = "x: " + Math.round(arrayAcc[i].getX() * 100) / 100.0 + "  y: " + Math.round(arrayAcc[i].getY() * 100) / 100.0 + "  z: " + Math.round(arrayAcc[i].getZ() * 100) / 100.0;
                    sensorMsg.setText(sensorMsgStr);

                    if (isRecording) {
                        try {
                            writer.append(resultStrRecord).append("\n");
                        } catch (IOException e) {
                            throw new RuntimeException(e);
                        }
                    }
                }
            }

            @Override
            public void onError(MdsException error) {
                //Log.e(LOG_TAG, "subscription onError(): ", error);
                unsubscribe();
            }
        });
    }


    /**
     * Unsubscribe from the sensor data
     */
    private void unsubscribe() {
        if (mdsSubscription != null) {
            mdsSubscription.unsubscribe();
            mdsSubscription = null;
        }
    }


    /**
     * When the activity is destroyed, unsubscribe from the sensor data
     */
    public void settingsButtonClicked(View view) {
        Intent intent = new Intent(RecordActivity.this, MainActivity.class);
        intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
        startActivity(intent);
    }


    /**
     * Set the current time to the sensor
     * This is used to synchronize the time between the phone and the sensor
     * The time is set to the current time in milliseconds
     *
     * @param serial: The serial number of the connected device
     */
    private void setCurrentTimeToSensor(String serial) {
        String timeUri = MessageFormat.format(URI_TIME, serial);
        String payload = "{\"value\":" + (new Date().getTime() * 1000) + "}";
        getMds().put(timeUri, payload, new MdsResponseListener() {
            @Override
            public void onSuccess(String data, MdsHeader header) {
                Log.i(LOG_TAG, "PUT /Time successful: " + data);
            }

            @Override
            public void onError(MdsException e) {
                Log.e(LOG_TAG, "PUT /Time returned error: " + e);
            }
        });
    }


    /**
     * When the activity is paused, stop recording if it's currently active and unsubscribe from sensor data
     */
    protected void onPause() {
        super.onPause();
        // Stop recording if it's currently active
        if (isRecording) {
            try {
                recordGestureButtonClicked(null); // Call the method to stop recording
            } catch (IOException e) {
                e.printStackTrace();
            }
        }
        // Unsubscribe from sensor data
        unsubscribe();
    }


    /**
     * When the activity is destroyed, unsubscribe from the sensor data
     */
    @Override
    protected void onDestroy() {
        super.onDestroy();
        // Stop recording if it's currently active
        if (isRecording) {
            try {
                recordGestureButtonClicked(null); // Call the method to stop recording
            } catch (IOException e) {
                e.printStackTrace();
            }
        }
        // Unsubscribe from sensor data when activity is destroyed
        unsubscribe();
    }

    public void backButtonClicked(View view) {
        finish();
    }
}