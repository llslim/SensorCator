package com.tanujn45.a11y;

import android.content.Intent;
import android.os.Bundle;
import android.os.Environment;
import android.speech.tts.TextToSpeech;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.ListView;
import android.widget.TextView;

import androidx.activity.result.ActivityResultLauncher;
import androidx.activity.result.contract.ActivityResultContracts;
import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.widget.SwitchCompat;
import androidx.core.content.ContextCompat;

import com.tanujn45.a11y.CSVEditor.CSVFile;

import java.io.File;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Objects;

public class GestureInstanceActivity extends AppCompatActivity {
    private String gestureName, speakableText, path, folderName;
    private File directory;
    TextView gestureCategoryNameTextView, speakableTextTextView;
    ListView gestureInstanceListView;
    ImageButton playTTSButton;
    List<String> instanceList = new ArrayList<>();
    ArrayAdapter<String> instanceAdapter;
    CSVFile master, subMaster;
    TextToSpeech tts;
    boolean noSpeakableText = false;

    private final ActivityResultLauncher<Intent> activityResultLauncher = registerForActivityResult(new ActivityResultContracts.StartActivityForResult(), result -> {
        if (result.getResultCode() == RESULT_OK) {
            Intent data = result.getData();
            if (data != null) {
                String instanceName = data.getStringExtra("refresh");
                if (instanceName.equals("refresh")) {
                    readSubMaster();
                    instanceList.clear();
                    setInstanceList();
                }
            }
        }
    });

    private void readSubMaster() {
        try {
            File subMasterFile = new File(path, "master_" + folderName + ".csv");
            if (!subMasterFile.exists()) {
                try {
                    subMaster = new CSVFile(new String[]{"Instance name", "Start time", "End time"}, subMasterFile.getAbsolutePath());
                } catch (Exception e) {
                    throw new RuntimeException(e);
                }
            } else {
                try {
                    subMaster = new CSVFile(subMasterFile.getAbsolutePath());
                } catch (Exception e) {
                    throw new RuntimeException(e);
                }
            }
        } catch (Exception e) {
            throw new RuntimeException(e);
        }
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_gesture_instance);

        Intent intent = getIntent();
        if (intent.hasExtra("gestureCategoryName")) {
            gestureName = intent.getStringExtra("gestureCategoryName");
        } else {
            // go back one activity
            Intent goBackIntent = new Intent(this, GestureCategoryActivity.class);
            startActivity(goBackIntent);
            finish();
        }

        directory = getExternalFilesDir(Environment.DIRECTORY_DOCUMENTS);
        folderName = gestureName.replace(" ", "_").toLowerCase();
        path = directory.getAbsolutePath() + "/trimmedData/" + folderName;

        File subMasterFile = new File(path, "master_" + folderName + ".csv");
        File masterFile = new File(directory, "master.csv");

        if (!subMasterFile.exists()) {
            try {
                subMaster = new CSVFile(new String[]{"Instance name", "Start time", "End time"}, subMasterFile.getAbsolutePath());
            } catch (Exception e) {
                throw new RuntimeException(e);
            }
        } else {
            try {
                subMaster = new CSVFile(subMasterFile.getAbsolutePath());
            } catch (Exception e) {
                throw new RuntimeException(e);
            }
        }

        try {
            master = new CSVFile(masterFile.getAbsolutePath());
        } catch (Exception e) {
            throw new RuntimeException(e);
        }

        tts = new TextToSpeech(this, status -> {
            if (status != TextToSpeech.ERROR) {
                tts.setLanguage(Locale.US);
            }
        });

        speakableText = master.getRowWithData(gestureName)[1];

        gestureCategoryNameTextView = findViewById(R.id.gestureCategoryName);
        speakableTextTextView = findViewById(R.id.speakableText);
        gestureInstanceListView = findViewById(R.id.gestureInstanceListView);
        playTTSButton = findViewById(R.id.playTTSButton);

        gestureCategoryNameTextView.setText(gestureName);
        if (speakableText == null || speakableText.isEmpty()) {
            speakableTextTextView.setText("No speakable text provided");
            noSpeakableText = true;
            speakableTextTextView.setTextColor(ContextCompat.getColor(this, R.color.theme));
        } else {
            speakableText = speakableText.replace("|", ",");
            speakableTextTextView.setText(speakableText);
        }

        setInstanceList();
    }

    private void setInstanceList() {
        boolean skipHeader = true;
        for (String[] row : subMaster.getCSVData()) {
            if (skipHeader) {
                skipHeader = false;
                continue;
            }
            instanceList.add(row[0]);
        }

        instanceAdapter = new ArrayAdapter<>(this, android.R.layout.simple_list_item_1, instanceList);
        gestureInstanceListView.setAdapter(instanceAdapter);

        gestureInstanceListView.setOnItemClickListener((parent, view, position, id) -> {
            String selectedItem = (String) parent.getItemAtPosition(position);
            startInstanceIntent(selectedItem);
        });
    }

    private void startInstanceIntent(String selectedItem) {
        Intent intent = new Intent(this, InstanceDataActivity.class);
        intent.putExtra("gestureCategoryName", gestureName);
        intent.putExtra("instanceName", selectedItem);
        activityResultLauncher.launch(intent);
    }

    public void addGestureInstance(View view) {
        Intent intent = new Intent(this, VideoListActivity.class);
        intent.putExtra("gestureCategoryName", gestureName);
        startActivity(intent);
        finish();
    }

    public void editGestureCategory(View view) {
        View dialogView = LayoutInflater.from(this).inflate(R.layout.gesture_category_edit_dialog, null);

        EditText gestureCategoryNameEditText = dialogView.findViewById(R.id.gestureCategoryNameEditText);
        EditText speakableTextEditText = dialogView.findViewById(R.id.speakableTextEditText);
        SwitchCompat ignoreGesture = dialogView.findViewById(R.id.ignoreGestureCategorySwitch);
        Button cancelButton = dialogView.findViewById(R.id.cancelButton);
        Button saveButton = dialogView.findViewById(R.id.saveButton);
        Button deleteButton = dialogView.findViewById(R.id.deleteButton);
        gestureCategoryNameEditText.setText(gestureName);
        if (!noSpeakableText) {
            speakableTextEditText.setText(speakableText);
        }
        ignoreGesture.setChecked(false);
        String toIgnore = master.getRowWithData(gestureName)[4];
        if (Objects.equals(toIgnore, "true")) {
            ignoreGesture.setChecked(true);
        }

        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setView(dialogView);
        AlertDialog alertDialog = builder.create();

        cancelButton.setOnClickListener(v -> alertDialog.dismiss());

        saveButton.setOnClickListener(v -> {
            // Change the name of the files inside the instances folder
            List<String[]> subMasterData = subMaster.getCSVData();
            String newInstanceSubstring = gestureCategoryNameEditText.getText().toString().toLowerCase().replace(" ", "_").trim();

            //Todo: change the name of the instances in the subMaster csv
            boolean skipHeader = true;
            for (String[] row : subMasterData) {
                if (skipHeader) {
                    skipHeader = false;
                    continue;
                }
                row[0] = row[0].replace(folderName, newInstanceSubstring);
            }
            subMaster.save();

            renameInstanceFiles(folderName, newInstanceSubstring);

            // Change the name of the folder
            File oldDir = new File(path);
            if (!oldDir.exists() || !oldDir.isDirectory()) {
                return;
            }

            File newDir = new File(oldDir.getParent(), newInstanceSubstring);
            if (oldDir.renameTo(newDir)) {
                System.out.println("Directory renamed");
            }
            path = path.replace(folderName, newInstanceSubstring);
            try {
                subMaster = new CSVFile(new File(path, "master_" + newInstanceSubstring + ".csv").getAbsolutePath());
            } catch (Exception e) {
               throw new RuntimeException(e);
            }

            String[] rowData = master.getRowWithData(gestureName);
            rowData[0] = gestureCategoryNameEditText.getText().toString().trim();
            rowData[1] = speakableTextEditText.getText().toString().trim().replace(",", "|");
            rowData[2] = path;
            rowData[4] = String.valueOf(ignoreGesture.isChecked());
            master.save();

            alertDialog.dismiss();
            Intent intent = new Intent(GestureInstanceActivity.this, GestureCategoryActivity.class);
            startActivity(intent);
            finish();
        });

        deleteButton.setOnClickListener(v -> {
            deleteDirectory(new File(path));
            master.deleteRowWithData(gestureName);
            master.save();
            alertDialog.dismiss();
//            Intent intent = new Intent(GestureInstanceActivity.this, GestureCategoryActivity.class);
//            startActivity(intent);
            finish();
        });

        alertDialog.show();
    }

    public static boolean deleteDirectory(File dir) {
        if (dir.isDirectory()) {
            String[] children = dir.list();
            if (children == null) {
                return false;
            }
            for (String child : children) {
                boolean success = deleteDirectory(new File(dir, child));
                if (!success) {
                    return false;
                }
            }
        }
        return dir.delete();
    }

    public void renameInstanceFiles(String oldFileName, String newFileName) {
        File instanceDirectory = new File(path);

        File[] files = instanceDirectory.listFiles();
        if (files == null) {
            return;
        }
        for (File file : files) {
            if (file.isFile()) {
                String fileName = file.getName().replace(oldFileName, newFileName);
                File newFile = new File(instanceDirectory + "/" + fileName);
                if (file.renameTo(newFile)) {
                    System.out.println("File renamed");
                }
            }
        }
    }

    public void playTTSButton(View view) {
        // play the text to speech
        tts.speak(speakableTextTextView.getText(), TextToSpeech.QUEUE_FLUSH, null, null);
    }

    public void backButtonClicked(View view) {
        finish();
    }
}
