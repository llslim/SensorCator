package com.tanujn45.a11y;

import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.media.MediaMetadataRetriever;
import android.os.Bundle;
import android.os.Environment;
import android.view.View;
import android.view.ViewGroup;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.Spinner;
import android.widget.Toast;
import android.widget.VideoView;

import androidx.appcompat.app.AppCompatActivity;
import androidx.constraintlayout.widget.ConstraintLayout;

import com.github.mikephil.charting.charts.LineChart;
import com.github.mikephil.charting.data.Entry;
import com.github.mikephil.charting.data.LineData;
import com.github.mikephil.charting.data.LineDataSet;
import com.skydoves.powerspinner.DefaultSpinnerAdapter;
import com.skydoves.powerspinner.OnSpinnerItemSelectedListener;
import com.skydoves.powerspinner.PowerSpinnerView;
import com.tanujn45.a11y.CSVEditor.CSVFile;
import com.tanujn45.a11y.filters.Filters;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;

public class VisualizationActivity extends AppCompatActivity {
    List<String> gestureNamesList = new ArrayList<>();
    HashMap<String, String> gestureNameToPath = new HashMap<>();
    HashMap<String, String> instanceNameToGestureName = new HashMap<>();
    VideoView videoView1, videoView2;
    File directory;
    ConstraintLayout instanceOneVideoLayout, instanceTwoVideoLayout;
    Spinner gestureOneSpinner, gestureTwoSpinner;
    ImageButton playBothVideosButton;
    ImageView gestureOneThumbnail, gestureTwoThumbnail;
    LineChart accChart;
    ArrayAdapter<String> adapterOne, adapterTwo, adapterFilter;
    String gestureOneVideoPath, gestureTwoVideoPath;
    String gestureOneCSVPath, gestureTwoCSVPath;
    String gestureOneCategory, gestureTwoCategory;
    String gestureOneInstance, gestureTwoInstance;
    String currFilter;
    PowerSpinnerView filterSpinner;
    Filters filters;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_visualization);

        directory = getExternalFilesDir(Environment.DIRECTORY_DOCUMENTS);

        gestureOneSpinner = findViewById(R.id.gestureOneSpinner);
        gestureTwoSpinner = findViewById(R.id.gestureTwoSpinner);
        filterSpinner = findViewById(R.id.filterSpinner);
        gestureOneThumbnail = findViewById(R.id.videoThumbnailOne);
        gestureTwoThumbnail = findViewById(R.id.videoThumbnailTwo);
        playBothVideosButton = findViewById(R.id.playBothVideos);
        instanceOneVideoLayout = findViewById(R.id.instanceOneVideoLayout);
        instanceTwoVideoLayout = findViewById(R.id.instanceTwoVideoLayout);
        videoView1 = findViewById(R.id.videoView1);
        videoView2 = findViewById(R.id.videoView2);
        accChart = findViewById(R.id.accLineChart);
        filters = findViewById(R.id.filters);

        initGestureDict();
        initSpinners();
        initVideoViews();
        setPlayBothVideosButtonWidth();

        currFilter = "acc";
    }

    private void setChart(LineChart chart, String filter, CSVFile instanceOneData, CSVFile instanceTwoData) {
        List<Entry> xOneEntries = new ArrayList<>();
        List<Entry> yOneEntries = new ArrayList<>();
        List<Entry> zOneEntries = new ArrayList<>();

        List<Entry> xTwoEntries = new ArrayList<>();
        List<Entry> yTwoEntries = new ArrayList<>();
        List<Entry> zTwoEntries = new ArrayList<>();


        List<String> xOneData = instanceOneData.getColumnData(filter + "_x");
        List<String> yOneData = instanceOneData.getColumnData(filter + "_y");
        List<String> zOneData = instanceOneData.getColumnData(filter + "_z");

        List<String> xTwoData = instanceTwoData.getColumnData(filter + "_x");
        List<String> yTwoData = instanceTwoData.getColumnData(filter + "_y");
        List<String> zTwoData = instanceTwoData.getColumnData(filter + "_z");

        int maxLength = Math.max(xOneData.size(), xTwoData.size());

        for (int i = 0; i < maxLength; i++) {
            if (i < xOneData.size()) {
                float x = Float.parseFloat(xOneData.get(i));
                xOneEntries.add(new Entry(i, x));
                float y = Float.parseFloat(yOneData.get(i));
                yOneEntries.add(new Entry(i, y));
                float z = Float.parseFloat(zOneData.get(i));
                zOneEntries.add(new Entry(i, z));
            }

            if (i < xTwoData.size()) {
                float x = Float.parseFloat(xTwoData.get(i));
                xTwoEntries.add(new Entry(i, x));
                float y = Float.parseFloat(yTwoData.get(i));
                yTwoEntries.add(new Entry(i, y));
                float z = Float.parseFloat(zTwoData.get(i));
                zTwoEntries.add(new Entry(i, z));
            }
        }

        LineDataSet xOneDataSet = new LineDataSet(xOneEntries, "X_1");
        xOneDataSet.setColor(Color.RED);
        xOneDataSet.setLineWidth(2f);
        xOneDataSet.setDrawCircles(false);
        LineDataSet yOneDataSet = new LineDataSet(yOneEntries, "Y_1");
        yOneDataSet.setColor(Color.GREEN);
        yOneDataSet.setLineWidth(2f);
        yOneDataSet.setDrawCircles(false);
        LineDataSet zOneDataSet = new LineDataSet(zOneEntries, "Z_1");
        zOneDataSet.setColor(Color.BLUE);
        zOneDataSet.setLineWidth(2f);
        zOneDataSet.setDrawCircles(false);

        LineDataSet xTwoDataSet = new LineDataSet(xTwoEntries, "X_2");
        xTwoDataSet.enableDashedLine(50f, 30f, 0f);
        xTwoDataSet.setColor(Color.RED);
        xTwoDataSet.setLineWidth(2f);
//        xTwoDataSet.setColor(Color.rgb(255, 165, 0));
        xTwoDataSet.setDrawCircles(false);
        LineDataSet yTwoDataSet = new LineDataSet(yTwoEntries, "Y_2");
        yTwoDataSet.enableDashedLine(50f, 30f, 0f);
        yTwoDataSet.setColor(Color.GREEN);
        yTwoDataSet.setLineWidth(2f);
//        yTwoDataSet.setColor(Color.rgb(128, 0, 128));
        yTwoDataSet.setDrawCircles(false);
        LineDataSet zTwoDataSet = new LineDataSet(zTwoEntries, "Z_2");
        zTwoDataSet.enableDashedLine(50f, 30f, 0f);
        zTwoDataSet.setColor(Color.BLUE);
        zTwoDataSet.setLineWidth(2f);
//        zTwoDataSet.setColor(Color.rgb(0, 255, 255));
        zTwoDataSet.setDrawCircles(false);

        LineData lineData = new LineData(xOneDataSet, yOneDataSet, zOneDataSet, xTwoDataSet, yTwoDataSet, zTwoDataSet);
        chart.setData(lineData);
        chart.getDescription().setEnabled(false);
        chart.setDrawMarkers(false);
        chart.invalidate();
    }

    private void setupGraph(String filter) {
        if (gestureOneCSVPath == null || gestureTwoCSVPath == null) {
            return;
        }
        CSVFile instanceOneData;
        CSVFile instanceTwoData;
        try {
            instanceOneData = new CSVFile(gestureOneCSVPath);
            instanceTwoData = new CSVFile(gestureTwoCSVPath);
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
        setChart(accChart, filter, instanceOneData, instanceTwoData);
    }

    private void setPlayBothVideosButtonWidth() {
        playBothVideosButton.post(new Runnable() {
            @Override
            public void run() {
                int height = playBothVideosButton.getHeight();

                ViewGroup.LayoutParams params = playBothVideosButton.getLayoutParams();
                params.width = height;
                playBothVideosButton.setLayoutParams(params);
            }
        });
    }

    private void initGestureDict() {
        String pathToTrimmedData = directory.getAbsolutePath() + "/trimmedData";
        File trimmedData = new File(pathToTrimmedData);

        if (!trimmedData.exists() || !trimmedData.isDirectory()) {
            throw new RuntimeException("Trimmed data directory does not exist");
        }

        File[] gestureDirectories = trimmedData.listFiles();

        if (gestureDirectories == null) {
            return;
        }

        for (File dir : gestureDirectories) {
            if (!dir.isDirectory()) {
                continue;
            }

            String dirName = dir.getName();

            File[] gestureFiles = dir.listFiles();
            if (gestureFiles == null) {
                continue;
            }

            for (File gesture : gestureFiles) {
                if (gesture.isDirectory()) {
                    continue;
                }

                String currGesture = gesture.getName();
                if (currGesture.startsWith("master")) {
                    continue;
                }

                if (currGesture.contains("handlandmarkerFile1234")) {
                    continue;
                }

                if (currGesture.endsWith(".csv")) {
                    String path = gesture.getAbsolutePath();
                    String name = "";

                    int lastDotIndex = currGesture.lastIndexOf(".");
                    if (lastDotIndex > 0) {
                        name = currGesture.substring(0, lastDotIndex);
                        gestureNamesList.add(name.replace("_", " ").trim());
                    }

                    gestureNameToPath.put(name, path);
                    instanceNameToGestureName.put(name, dirName);
                }
            }
        }

        if (gestureNamesList.size() == 0) {
            Toast.makeText(this, "No gestures found", Toast.LENGTH_SHORT).show();
            finish();
        }
    }

    private void initSpinners() {
        adapterOne = new ArrayAdapter<>(this, android.R.layout.simple_spinner_item, gestureNamesList);
        adapterOne.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        gestureOneSpinner.setAdapter(adapterOne);
        gestureOneSpinner.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
                String selectedItem = (String) parent.getItemAtPosition(position);
                selectedItem = selectedItem.replace(" ", "_").toLowerCase();
                filters.setGesture1(selectedItem);
                gestureOneInstance = selectedItem;
                gestureOneCategory = instanceNameToGestureName.get(selectedItem);
                String path = gestureNameToPath.get(selectedItem);
                gestureOneCSVPath = path;
                gestureOneVideoPath = path.replace(".csv", ".mp4");
                videoView1.setVideoPath(gestureOneVideoPath);
                setupGraph(currFilter);
                setVideoThumbnail(gestureOneThumbnail, gestureOneVideoPath);
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) {
                // Do nothing
            }
        });

        adapterTwo = new ArrayAdapter<>(this, android.R.layout.simple_spinner_item, gestureNamesList);
        adapterTwo.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        gestureTwoSpinner.setAdapter(adapterTwo);
        gestureTwoSpinner.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override
            public void onItemSelected(AdapterView<?> parent, View view, int position, long id) {
                String selectedItem = (String) parent.getItemAtPosition(position);
                selectedItem = selectedItem.replace(" ", "_").toLowerCase();
                filters.setGesture2(selectedItem);
                gestureTwoInstance = selectedItem;
                gestureTwoCategory = instanceNameToGestureName.get(selectedItem);
                String path = gestureNameToPath.get(selectedItem);
                gestureTwoCSVPath = path;
                gestureTwoVideoPath = path.replace(".csv", ".mp4");
                videoView2.setVideoPath(gestureTwoVideoPath);
                setupGraph(currFilter);
                setVideoThumbnail(gestureTwoThumbnail, gestureTwoVideoPath);
            }

            @Override
            public void onNothingSelected(AdapterView<?> parent) {
                // Do nothing
            }
        });

        DefaultSpinnerAdapter adapterFilter = new DefaultSpinnerAdapter(filterSpinner);
        List<CharSequence> items = Arrays.asList("Acc", "Acc MA", "Gyro");
        adapterFilter.setItems(items);
        filterSpinner.setSpinnerAdapter(adapterFilter);
        filterSpinner.selectItemByIndex(0);

        filterSpinner.setOnSpinnerItemSelectedListener((OnSpinnerItemSelectedListener<String>) (oldIndex, oldItem, newIndex, newItem) -> {
            newItem = newItem.toLowerCase().replace(" ", "_");
            currFilter = newItem;
            setupGraph(currFilter);
        });

    }

    private Bitmap getVideoThumbnail(String videoPath) throws IOException {
        MediaMetadataRetriever retriever = new MediaMetadataRetriever();
        try {
            retriever.setDataSource(videoPath);
            return retriever.getFrameAtTime(0, MediaMetadataRetriever.OPTION_CLOSEST_SYNC);
        } catch (Exception e) {
            throw new RuntimeException(e);
        } finally {
            retriever.release();
        }
    }

    private void initVideoViews() {
        videoView1.setOnPreparedListener(mp -> {
            mp.setVolume(0, 0);
        });

        videoView2.setOnPreparedListener(mp -> {
            mp.setVolume(0, 0);
        });

        videoView1.setOnCompletionListener(mp -> {
            instanceOneVideoLayout.setVisibility(View.VISIBLE);
        });

        videoView2.setOnCompletionListener(mp -> {
            instanceTwoVideoLayout.setVisibility(View.VISIBLE);
        });
    }

    private void setVideoThumbnail(ImageView imageView, String videoPath) {
        try {
            Bitmap thumbnail = getVideoThumbnail(videoPath);
            imageView.setImageBitmap(thumbnail);
        } catch (IOException e) {
            throw new RuntimeException(e);
        }
    }

    public void playInstanceVideoOne(View view) {
        Intent intent = new Intent(this, InstanceDataActivity.class);
        intent.putExtra("instanceName", gestureOneInstance);
        intent.putExtra("gestureCategoryName", gestureOneCategory);
        intent.putExtra("notFromInstanceGestureIntent", true);
        intent.putExtra("videoPath", gestureOneVideoPath);
        startActivity(intent);
    }

    public void playInstanceVideoTwo(View view) {
        Intent intent = new Intent(this, InstanceDataActivity.class);
        intent.putExtra("instanceName", gestureTwoInstance);
        intent.putExtra("gestureCategoryName", gestureTwoCategory);
        intent.putExtra("notFromInstanceGestureIntent", true);
        intent.putExtra("videoPath", gestureTwoVideoPath);
        startActivity(intent);
    }

    public void playBothVideos(View view) {
        int maxDuration = Math.max(videoView1.getDuration(), videoView2.getDuration());
        instanceOneVideoLayout.setVisibility(View.INVISIBLE);
        instanceTwoVideoLayout.setVisibility(View.INVISIBLE);

        videoView1.seekTo(0);
        videoView2.seekTo(0);

        videoView1.start();
        videoView2.start();

        accChart.animateX(maxDuration);

    }

    public void backButtonClicked(View view) {
        finish();
    }
}