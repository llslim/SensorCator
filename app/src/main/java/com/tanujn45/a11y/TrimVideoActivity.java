package com.tanujn45.a11y;

import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Environment;
import android.text.TextUtils;
import android.util.Log;
import android.view.View;
import android.webkit.MimeTypeMap;
import android.widget.ProgressBar;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;

import com.google.mediapipe.tasks.components.containers.NormalizedLandmark;
import com.google.mediapipe.tasks.vision.core.RunningMode;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarkerResult;
import com.tanujn45.a11y.CSVEditor.CSVFile;
import com.tanujn45.a11y.VideoTrimmer.VideoTrimmer;
import com.tanujn45.a11y.VideoTrimmer.interfaces.OnTrimVideoListener;

import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.List;
import java.util.Locale;
import java.util.Objects;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;

public class TrimVideoActivity extends AppCompatActivity implements OnTrimVideoListener {
    private VideoTrimmer mVideoTrimmer;
    private File directory;
    private String videoPath, gestureName, folderName, path;
    CSVFile master, subMaster;
    private ScheduledExecutorService backgroundExecutor;
    ProgressBar progressBar;
    View overlayView;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_trim_video);

        progressBar = findViewById(R.id.progress);
        overlayView = findViewById(R.id.overlayView);

        videoPath = getIntent().getStringExtra("videoPath");
        gestureName = getIntent().getStringExtra("gestureCategoryName");

        directory = getExternalFilesDir(Environment.DIRECTORY_DOCUMENTS);
        folderName = gestureName.replace(" ", "_").toLowerCase();
        path = directory.getAbsolutePath() + "/trimmedData/" + folderName;
        File trimmedVideosDir = new File(path);

        try {
            master = new CSVFile(new File(directory, "master.csv").getAbsolutePath());
            subMaster = new CSVFile(new File(path + "/master_" + folderName + ".csv").getAbsolutePath());
        } catch (Exception e) {
            throw new RuntimeException(e);
        }

        mVideoTrimmer = findViewById(R.id.videoTrimmer);
        if (mVideoTrimmer != null) {
            mVideoTrimmer.setMaxDuration(60);
            mVideoTrimmer.setOnTrimVideoListener(this);
            mVideoTrimmer.setOnTrimVideoListener(this);
            mVideoTrimmer.setDestinationPath(trimmedVideosDir.getPath() + "/");
            mVideoTrimmer.setVideoURI(Uri.parse(videoPath));
        }
    }

    private int getInstanceCount() {
        String[] row = master.getRowWithData(gestureName);
        return Integer.parseInt(row[row.length - 2]);
    }

    private void incrementInstanceCount() {
        String[] row = master.getRowWithData(gestureName);
        int count = Integer.parseInt(row[row.length - 2]);
        count++;
        row[row.length - 2] = String.valueOf(count);
        if(master.editRowWithData(gestureName, row)) {
            master.save();
        }
    }

    @Override
    public void onTrimStarted() {

    }

    @Override
    public void getResult(Uri uri, int startPostion, int endPostion) {
        File file = new File(Objects.requireNonNull(uri.getPath()));
        String fileName = folderName + "_" + (getInstanceCount() + 1);
        File newFile = new File(path + "/" + fileName + ".mp4");

        if (file.renameTo(newFile)) {
            System.out.println("File is moved successful!");
            incrementInstanceCount();

        } else {
            System.out.println("File failed to move!");
            // Handle this case
        }

        // Trim the csv and apply the filters
        String csvPath = videoPath.replace(".mp4", ".csv").replace("Videos", "Data");
        File rawCSV = new File(csvPath);
        File newCSV = new File(path + "/" + fileName + ".csv");

        // Generate hand landmarks from the video


        try {
            Path sourcePath = Paths.get(rawCSV.getAbsolutePath());
            Path destinationPath = Paths.get(newCSV.getAbsolutePath());
            Files.copy(sourcePath, destinationPath);

            System.out.println("File copied successfully!");

        } catch (IOException e) {
            System.out.println("Failed to copy file: " + e.getMessage());
        }

        try {
            CSVFile csvFile = new CSVFile(newCSV.getAbsolutePath());

            csvFile.printData(0);
            csvFile.printData(2);

            csvFile.applyTime();
            csvFile.printData(0);
            csvFile.printData(2);

            csvFile.trimData(startPostion, endPostion);
            csvFile.applyMovingAverage();
            csvFile.applyDifferentiation();
            csvFile.save();
            double startTimeInSeconds = (double) startPostion / 1000.0;
            double endTimeInSeconds = (double) endPostion / 1000.0;
            subMaster.addRow(new String[]{fileName, String.valueOf(startTimeInSeconds), String.valueOf(endTimeInSeconds)});
            subMaster.save();
        } catch (Exception e) {
            throw new RuntimeException(e);
        }

//        processVideosAndGenerateCSV(newFile.getPath());

        Intent intent = new Intent(this, GestureInstanceActivity.class);
        intent.putExtra("gestureCategoryName", gestureName);
        startActivity(intent);
        finish();
    }

    public enum MediaType {
        IMAGE, VIDEO, UNKNOWN
    }


    private MediaType loadMediaType(Uri uri) {
        // First, try content resolver
        String mimeType = this.getContentResolver().getType(uri);
        if (mimeType != null) {
            if (mimeType.startsWith("image")) {
                return MediaType.IMAGE;
            }
            if (mimeType.startsWith("video")) {
                return MediaType.VIDEO;
            }
        }

        // Fallback to MimeTypeMap
        String fileExtension = MimeTypeMap.getFileExtensionFromUrl(uri.toString());
        if (!TextUtils.isEmpty(fileExtension)) {
            mimeType = MimeTypeMap.getSingleton().getMimeTypeFromExtension(fileExtension);
            if (mimeType != null) {
                if (mimeType.startsWith("video")) {
                    return MediaType.VIDEO;
                }
                if (mimeType.startsWith("image")) {
                    return MediaType.IMAGE;
                }
            }
        }

        return MediaType.UNKNOWN;
    }


    private void processVideosAndGenerateCSV(String videoPath) {
        MediaType type = loadMediaType(Uri.parse(videoPath));
        if (type != MediaType.VIDEO) {
            Toast.makeText(this, "Invalid video file", Toast.LENGTH_SHORT).show();
        }

        File videoFile = new File(videoPath);

        backgroundExecutor = Executors.newSingleThreadScheduledExecutor();
        backgroundExecutor.execute(() -> {

            Uri videoUri = Uri.fromFile(videoFile);
            String fileName = videoFile.getName();

            String videoFileName = videoUri.getLastPathSegment();

            this.runOnUiThread(() -> {
                Toast.makeText(this, "Processing: " + videoFileName, Toast.LENGTH_SHORT).show();
                progressBar.setVisibility(View.VISIBLE);
                overlayView.setVisibility(View.VISIBLE);
            });

            HandLandmarkerHelper handLandmarkerHelper = new HandLandmarkerHelper(this, null, RunningMode.VIDEO);

            HandLandmarkerHelper.ResultBundle resultBundle = handLandmarkerHelper.detectVideoFile(videoUri, 300L);

            if (resultBundle == null) {
                Log.e("HandLandmarker", "Error processing video: " + fileName);
                this.runOnUiThread(() -> Toast.makeText(this, "Error processing " + fileName, Toast.LENGTH_SHORT).show());
                handLandmarkerHelper.clearHandLandmarker();
            }

            String csvFileName = fileName.replace(".mp4", "_handlandmarkerFile1234.csv");
            File csvFile = new File(videoFile.getParentFile(), csvFileName);

            try {
                FileWriter csvWriter = new FileWriter(csvFile);
                // Write CSV header with landmark indices
                StringBuilder header = new StringBuilder("frame,hand_present");
                for (int i = 0; i < 21; i++) {
                    header.append(",x_").append(i).append(",y_").append(i).append(",z_").append(i);
                }
                header.append("\n");
                csvWriter.append(header.toString());

                for (int frameIdx = 0; frameIdx < resultBundle.getResults().size(); frameIdx++) {
                    HandLandmarkerResult result = resultBundle.getResults().get(frameIdx);
                    List<List<NormalizedLandmark>> landmarks = result.landmarks();
                    String frameHeader = String.format(Locale.US, "%d,%d", frameIdx, landmarks.size());

                    if (landmarks.size() == 0) {
                        frameHeader += "\n";
                        csvWriter.append(frameHeader);
                        continue;
                    }
                    for (NormalizedLandmark normalizedLandmark : landmarks.get(0)) {
                        frameHeader += String.format(Locale.US, ",%.4f,%.4f,%.4f", normalizedLandmark.x(), normalizedLandmark.y(), normalizedLandmark.z());
                    }

                    frameHeader += "\n";
                    csvWriter.append(frameHeader);
                }

                csvWriter.flush();
                csvWriter.close();

                this.runOnUiThread(() -> Toast.makeText(this, "CSV generated: " + csvFileName, Toast.LENGTH_SHORT).show());
            } catch (IOException e) {
                Log.e("CSV", "Error writing CSV file for " + fileName, e);
                this.runOnUiThread(() -> Toast.makeText(this, "Error generating CSV for " + fileName, Toast.LENGTH_SHORT).show());
            }
            handLandmarkerHelper.clearHandLandmarker();


            this.runOnUiThread(() -> {
                progressBar.setVisibility(View.GONE);
                overlayView.setVisibility(View.GONE);
                Toast.makeText(this, "All videos processed", Toast.LENGTH_LONG).show();
            });

//            Intent intent = new Intent(this, GestureInstanceActivity.class);
//            intent.putExtra("gestureCategoryName", gestureName);
//            startActivity(intent);
//            finish();
        });
    }

    @Override
    public void cancelAction() {
        Intent intent = new Intent(this, GestureInstanceActivity.class);
        intent.putExtra("gestureCategoryName", gestureName);
        startActivity(intent);
        finish();
    }

    @Override
    public void onError(String s) {

    }

    public void backButtonClicked(View view) {
        finish();
    }
}
