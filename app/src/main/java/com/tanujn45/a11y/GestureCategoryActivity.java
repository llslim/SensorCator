package com.tanujn45.a11y;

import android.content.Intent;
import android.os.Bundle;
import android.os.Environment;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ListView;
import android.widget.Toast;

import androidx.appcompat.app.AlertDialog;
import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.widget.SwitchCompat;

import com.tanujn45.a11y.CSVEditor.CSVFile;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

public class git add GestureCategoryActivity extends AppCompatActivity {

    private File directory;
    private CSVFile master;
    private List<String> categoryList = new ArrayList<>();
    private ArrayAdapter<String> categoryAdapter;
    ListView gestureCategoryListView;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_gesture_category);

        gestureCategoryListView = findViewById(R.id.gestureCategoryListView);

        directory = getExternalFilesDir(Environment.DIRECTORY_DOCUMENTS);
        File masterFile = new File(directory, "master.csv");
        if (!masterFile.exists()) {
            try {
                master = new CSVFile(new String[]{"Gesture Category Name", "Speakable Text", "Path", "Instance Count", "Ignore"}, masterFile.getAbsolutePath());
            } catch (Exception e) {
                throw new RuntimeException(e);
            }
        } else {
            try {
                master = new CSVFile(masterFile.getAbsolutePath());
            } catch (Exception e) {
                throw new RuntimeException(e);
            }
        }

        makeRestCategory();
        setGestureList();
    }

    /*
        Rest category is a special category which is always present and cannot be deleted.
        It is used to represent the rest state of the user.
    */
    void makeRestCategory() {
        if (master.checkIfDataExistsInARow("Rest")) {
            return;
        }
        File gestureCategoryFolder = new File(directory, "trimmedData/rest");
        if (!gestureCategoryFolder.exists()) {
            gestureCategoryFolder.mkdirs();
        }
        master.addRow(new String[]{"Rest", "", gestureCategoryFolder.getAbsolutePath(), "0", String.valueOf(true)});
        master.save();
    }

    /*
        Note: GestureInstance Activity expects the following intent:
        gestureCategoryName: String
        speakableText: String
    */
    private void setGestureList() {
        // Test this for empty master file
        boolean skipHeader = true;
        for (String[] row : master.getCSVData()) {
            if (skipHeader) {
                skipHeader = false;
                continue;
            }

            categoryList.add(row[0]);
        }
        categoryAdapter = new ArrayAdapter<>( this, android.R.layout.simple_list_item_1, categoryList);
        gestureCategoryListView.setAdapter(categoryAdapter);

        gestureCategoryListView.setOnItemClickListener((parent, view, position, id) -> {
            String selectedItem = (String) parent.getItemAtPosition(position);
            startInstanceIntent(selectedItem, master.getCSVData().get(position + 1)[1]);
        });
    }

    private void startGestureInstanceActivityWithDialog() {
        View dialogView = LayoutInflater.from(this).inflate(R.layout.gesture_category_dialog, null);

        EditText gestureCategoryNameEditText = dialogView.findViewById(R.id.gestureCategoryNameEditText);
        EditText speakableTextEditText = dialogView.findViewById(R.id.speakableTextEditText);
        SwitchCompat ignoreGesture = dialogView.findViewById(R.id.ignoreGestureCategorySwitch);
        Button cancelButton = dialogView.findViewById(R.id.cancelButton);
        Button saveButton = dialogView.findViewById(R.id.saveButton);

        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        builder.setView(dialogView);
        AlertDialog alertDialog = builder.create();

        cancelButton.setOnClickListener(v -> alertDialog.dismiss());

        saveButton.setOnClickListener(v -> {
            if (gestureCategoryNameEditText.getText().toString().isEmpty()) {
                Toast.makeText(this, "Gesture Category Name is required", Toast.LENGTH_SHORT).show();
                return;
            }
            String gestureCategoryName = gestureCategoryNameEditText.getText().toString().trim();
            String speakableText = speakableTextEditText.getText().toString().trim().replace(",", "|");

            if (master.checkIfDataExistsInARow(gestureCategoryName)) {
                Toast.makeText(this, "Gesture Category already exists", Toast.LENGTH_SHORT).show();
            } else {
                File gestureCategoryFolder = new File(directory, "trimmedData/" + gestureCategoryName.toLowerCase().replace(" ", "_"));
                if (!gestureCategoryFolder.exists()) {
                    gestureCategoryFolder.mkdirs();
                }
                boolean toIgnore = ignoreGesture.isChecked();
                master.addRow(new String[]{gestureCategoryName, speakableText, gestureCategoryFolder.getAbsolutePath(), "0", String.valueOf(toIgnore)});
                master.save();

                startInstanceIntent(gestureCategoryName, speakableText);
                finish();

                alertDialog.dismiss();
            }
        });

        alertDialog.show();
    }

    private void startInstanceIntent(String gestureCategoryName, String speakableText) {
        Intent intent = new Intent(GestureCategoryActivity.this, GestureInstanceActivity.class);
        intent.putExtra("gestureCategoryName", gestureCategoryName);
        startActivity(intent);
    }

    public void addGestureCategory(View view) {
        System.out.println("Adding gesture category");
        startGestureInstanceActivityWithDialog();
    }

    public void backButtonClicked(View view) {
        finish();
    }
}
