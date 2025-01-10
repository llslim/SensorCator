package com.tanujn45.a11y;

import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.media.MediaMetadataRetriever;
import android.os.Bundle;
import android.os.Environment;
import android.view.View;
import android.widget.ImageButton;
import android.widget.ImageView;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;

import com.github.mikephil.charting.charts.LineChart;
import com.github.mikephil.charting.data.Entry;
import com.github.mikephil.charting.data.LineData;
import com.github.mikephil.charting.data.LineDataSet;
import com.tanujn45.a11y.CSVEditor.CSVFile;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

public class InstanceDataActivity extends AppCompatActivity {
    private LineChart accChart, accMAChart, gyroChart;
    private TextView instanceNameTextView;
    private ImageView instanceImageView;
    private ImageButton deleteInstanceImageButton;
    private File directory;
    private String instanceName, gestureName, path, folderName, videoPath;
    private CSVFile subMaster, instanceData;
    private float startTime, endTime;

    // Expects instanceName and gestureCategoryName as extra intent

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_instance_data);

        directory = getExternalFilesDir(Environment.DIRECTORY_DOCUMENTS);

        Intent intent = getIntent();
        if (intent.hasExtra("instanceName") && intent.hasExtra("gestureCategoryName")) {
            instanceName = intent.getStringExtra("instanceName");
            gestureName = intent.getStringExtra("gestureCategoryName");
        } else {
            Intent goBackIntent = new Intent(this, GestureCategoryActivity.class);
            startActivity(goBackIntent);
            finish();
        }

        instanceName = intent.getStringExtra("instanceName");
        gestureName = intent.getStringExtra("gestureCategoryName");

        folderName = gestureName.replace(" ", "_").toLowerCase();
        path = directory.getAbsolutePath() + "/trimmedData/" + folderName;
        videoPath = directory.getAbsolutePath() + "/trimmedData/" + folderName + "/" + instanceName + ".mp4";

        File subMasterFile = new File(path, "master_" + folderName + ".csv");

        try {
            subMaster = new CSVFile(subMasterFile.getAbsolutePath());
            instanceData = new CSVFile(new File(path, instanceName + ".csv").getAbsolutePath());
        } catch (Exception e) {
            throw new RuntimeException(e);
        }

        getStartEndTime();

        instanceNameTextView = findViewById(R.id.instanceName);
        instanceNameTextView.setText(instanceName.replace("_", " "));
        instanceImageView = findViewById(R.id.videoThumbnail);
        deleteInstanceImageButton = findViewById(R.id.deleteInstanceImageButton);

        if (intent.hasExtra("notFromInstanceGestureIntent")) {
            deleteInstanceImageButton.setVisibility(View.GONE);
        }

        Bitmap thumbnail;
        try {
            thumbnail = getVideoThumbnail(videoPath);
        } catch (IOException e) {
            throw new RuntimeException(e);
        }

        if (thumbnail != null) {
            instanceImageView.setImageBitmap(thumbnail);
        }

        accChart = findViewById(R.id.accLineChart);
        accMAChart = findViewById(R.id.accMALineChart);
        gyroChart = findViewById(R.id.gyroLineChart);

        setChart(accChart, "acc");
        setChart(accMAChart, "acc_ma");
        setChart(gyroChart, "gyro");
    }

    private boolean deleteFileOnPath(String path) {
        File file = new File(path);
        return file.delete();
    }


    // Todo: HandlandmarkerFile1234 will be deleted here
    public void deleteInstance(View view) throws Exception {
        if (deleteFileOnPath(videoPath) && deleteFileOnPath(videoPath.replace(".mp4", ".csv"))) {

            //  && deleteFileOnPath(videoPath.replace(".mp4", "_handlandmarkerFile1234.csv"))
            subMaster.deleteRowWithData(instanceName);
            subMaster.save();

//            Intent intent = new Intent(this, GestureCategoryActivity.class);
//            startActivity(intent);
            Intent resultIntent = new Intent();
            resultIntent.putExtra("refresh", "refresh");
            setResult(RESULT_OK, resultIntent);
            finish();
        } else {
            throw new Exception("Failed to delete the instance");
        }
    }

    private Bitmap getVideoThumbnail(String videopath) throws IOException {
        MediaMetadataRetriever retriever = new MediaMetadataRetriever();
        try {
            retriever.setDataSource(videopath);
            return retriever.getFrameAtTime(0, MediaMetadataRetriever.OPTION_CLOSEST_SYNC);
        } catch (Exception e) {
            throw new RuntimeException(e);
        } finally {
            retriever.release();
        }
    }

    private void getStartEndTime() {
        String[] row = subMaster.getRowWithData(instanceName);
        startTime = Float.parseFloat(row[1]);
        endTime = Float.parseFloat(row[2]);
    }

    private void setChart(LineChart chart, String filter) {
        List<Entry> xEntries = new ArrayList<>();
        List<Entry> yEntries = new ArrayList<>();
        List<Entry> zEntries = new ArrayList<>();

        List<String> xData = instanceData.getColumnData(filter + "_x", startTime, endTime);
        List<String> yData = instanceData.getColumnData(filter + "_y", startTime, endTime);
        List<String> zData = instanceData.getColumnData(filter + "_z", startTime, endTime);

        for(int i = 0; i < xData.size(); i++) {
            float x = Float.parseFloat(xData.get(i));
            float y = Float.parseFloat(yData.get(i));
            float z = Float.parseFloat(zData.get(i));

            xEntries.add(new Entry(i, x));
            yEntries.add(new Entry(i, y));
            zEntries.add(new Entry(i, z));
        }

        LineDataSet xDataSet = new LineDataSet(xEntries, "X");
        xDataSet.setColor(Color.RED);
        xDataSet.setDrawCircles(false);
        LineDataSet yDataSet = new LineDataSet(yEntries, "Y");
        yDataSet.setColor(Color.GREEN);
        yDataSet.setDrawCircles(false);
        LineDataSet zDataSet = new LineDataSet(zEntries, "Z");
        zDataSet.setColor(Color.BLUE);
        zDataSet.setDrawCircles(false);

        LineData lineData = new LineData(xDataSet, yDataSet, zDataSet);
        chart.setData(lineData);
        chart.getDescription().setEnabled(false);
        chart.setDrawMarkers(false);

        chart.invalidate();
    }

    public void playInstanceVideo(View view) {
        Intent intent = new Intent(this, VideoPlayer.class);
        intent.putExtra("videoPath", videoPath);
        startActivity(intent);
    }

    public void backButtonClicked(View view) {
        finish();
    }
}
