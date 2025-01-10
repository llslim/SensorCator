package com.tanujn45.a11y.KMeans;

import android.content.Context;
import android.os.Environment;

import com.tanujn45.a11y.CSVEditor.CSVFile;

import java.io.File;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;

import weka.clusterers.SimpleKMeans;
import weka.core.Attribute;
import weka.core.DenseInstance;
import weka.core.Instance;
import weka.core.Instances;


/*
Documentation:

Set the model for performing the kMeans clustering

The class is primarily used for two purposes.
1. Generate similarity matrix for the purpose of generating heatmap.
2. Perform kMeans clustering on unknown data and return the result.

When we call the set model function. The class stores the prefixes
and weights for the model and the perform kMeans to generate kMeans
models for each prefix.
So everytime we have to call the
setModel() function.
Now if we want to generate the similarity matrix we can call the
we simply call
performKMeans() function. This function will return a 2D similarity
matrix after the gesture names are sorted in ascending order.

If we want to perform kMeans on unknown data we can call the
performKMeans() function with the data as the parameter.
It accepts the data either in the form of a Weka instance or
a list of values. These values are
timestamp, acc_x, acc_y, acc_z, gyro_x, gyro_y, gyro_z

Other important functions
1. getCSVFileNames() - Returns the list of gesture names
2. getModelNames() - Returns the list of model names
3. setNClusters() - Sets the number of clusters for kMeans
4. createInstance() - Creates a Weka instance
 */

// First set model
// Set KMeans
public class KMeans {
    private final File directory;
    private final List<File> csvFiles;
    private final List<File> models;
    private HashMap<String, Integer> csvFileNameToGestureId;
    private String[] csvFileNames;
    private String[] prefixes;
    private double[] weights;
    private final List<SimpleKMeans> kMeans;
    private Instances combinedGesturesData;
    private int nClusters = 20;
    private final String trimmedDataFolderPath;
    private final String modelsPath;
    private Instances sensorInstances;

    public KMeans(Context context) {
        this.directory = context.getExternalFilesDir(Environment.DIRECTORY_DOCUMENTS);
        this.trimmedDataFolderPath = this.directory + "/trimmedData/";
        this.modelsPath = this.directory + "/models/";
        this.csvFiles = new ArrayList<>();
        this.kMeans = new ArrayList<>();
        this.models = new ArrayList<>();
        this.csvFileNameToGestureId = new HashMap<>();
        this.setModels();
        this.setSensorInstancesAttributes();
    }

    private void setSensorInstancesAttributes() {
        ArrayList<Attribute> attributes = new ArrayList<>();
        attributes.add(new Attribute("timestamp"));
        attributes.add(new Attribute("acc_x"));
        attributes.add(new Attribute("acc_y"));
        attributes.add(new Attribute("acc_z"));
        attributes.add(new Attribute("gyro_x"));
        attributes.add(new Attribute("gyro_y"));
        attributes.add(new Attribute("gyro_z"));
        attributes.add(new Attribute("acc_diff_x"));
        attributes.add(new Attribute("acc_diff_y"));
        attributes.add(new Attribute("acc_diff_z"));
        attributes.add(new Attribute("acc_ma_x"));
        attributes.add(new Attribute("acc_ma_y"));
        attributes.add(new Attribute("acc_ma_z"));
        this.sensorInstances = new Instances("sensorInstances", attributes, 0);
    }

    public void setNClusters(int nClusters) {
        this.nClusters = nClusters;
    }

    public void setModel(String modelName) {
        modelName = modelName.trim();
        if (!modelName.endsWith(".csv")) {
            modelName += ".csv";
        }

        File currModel = null;
        for (File model : this.models) {
            if (!model.getName().equals(modelName)) {
                continue;
            }
            this.kMeans.clear();
            currModel = model;
            break;
        }

        if (currModel == null) {
            throw new RuntimeException("Model not found!");
        }

        this.setPrefixesAndWeights(currModel);
    }

    private void setPrefixesAndWeights(File model) {
        CSVFile modelCSV;
        try {
            modelCSV = new CSVFile(model.getAbsolutePath());
        } catch (Exception e) {
            e.printStackTrace();
            return;
        }

        this.prefixes = new String[modelCSV.getCSVData().size() - 1];
        this.weights = new double[modelCSV.getCSVData().size() - 1];

        List<String[]> data = modelCSV.getCSVData();
        for (int i = 1; i < data.size(); i++) {
            this.prefixes[i - 1] = data.get(i)[0];
            this.weights[i - 1] = Double.parseDouble(data.get(i)[1]);
        }
        try {
            this.getKMeans();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private void setPrefixesAndWeights(String[] prefixes, double[] weights) {
        this.prefixes = prefixes;
        this.weights = weights;

        try {
            this.getKMeans();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private void setModels() {
        File dir = new File(this.modelsPath);
        if (!dir.isDirectory()) {
            throw new RuntimeException("Models Folder does not exist!");
        }

        File[] files = dir.listFiles();
        if (files == null) {
            return;
        }
        for (File file : files) {
            if (file.isFile() && file.getName().endsWith(".csv")) {
                this.models.add(file);
            }
        }
    }

    // Returns a list of model names
    public List<String> getModelNames() {
        List<String> modelNames = new ArrayList<>();
        for (File model : this.models) {
            modelNames.add(model.getName());
        }
        return modelNames;
    }

    // Loads data from a File and returns an Instances object
//    private Instances loadData(File file) throws IOException {
//        if (!file.exists()) {
//            System.out.println("File does not exist");
//        }
//        CSVLoader loader = new CSVLoader();
//        loader.setSource(file);
//        Instances data = loader.getDataSet();
//        return data;
//    }

    private Instances loadData(File file, int gestureId) {
        // Create a CSV file object
        String filePath = file.getAbsolutePath();
        CSVFile dataFile = null;
        try {
            dataFile = new CSVFile(filePath);
        } catch (Exception e) {
            e.printStackTrace();
        }

        // Retrieve all the data from the CSV file
        assert dataFile != null;
        List<String[]> data = dataFile.getCSVData();

        // Create an arraylist of attributes
        String[] header = data.get(0);
        ArrayList<Attribute> attributes = new ArrayList<>();
        for (String s : header) {
            attributes.add(new Attribute(s));
        }
        attributes.add(new Attribute("gesture_id"));

        // Create an instance with the attributes
        Instances dataInstances = new Instances(file.getName(), attributes, 0);

        // Add the data to the instance
        int numAttributes = attributes.size();
        for (int i = 1; i < data.size(); i++) {
            DenseInstance instance = new DenseInstance(numAttributes);
            for (int j = 0; j < numAttributes - 1; j++) {
                instance.setValue(j, Double.parseDouble(data.get(i)[j]));
            }

            instance.setValue(numAttributes - 1, (double) gestureId);
            dataInstances.add(instance);
        }

        return dataInstances;
    }

    // Sets the list of gesture CSV files in the sub-directories in trimmed data directory
    private List<String> getCSVFiles() {
        // Check if the trimmed data folder exists
        File dir = new File(this.trimmedDataFolderPath);
        if (!dir.isDirectory()) {
            throw new RuntimeException("Trimmed Data Folder does not exist!");
        }

        // Get all the Gesture directories
        File[] subDirs = dir.listFiles();
        if (subDirs == null) {
            return null;
        }

        List<String> tempCSVFileNames = new ArrayList<>();
        for (File subDir : subDirs) {
            if (!subDir.isDirectory()) {
                continue;
            }

            // Get all the files in the sub-directory
            File[] files = subDir.listFiles();
            if (files == null) {
                continue;
            }

            for (File file : files) {
                String fileName = file.getName();
                if (fileName.startsWith("master")) {
                    continue;
                }

                if (fileName.contains("handlandmarkerFile1234")) {
                    continue;
                }

                if (file.isFile() && fileName.endsWith(".csv")) {
                    this.csvFiles.add(file);
                    tempCSVFileNames.add(fileName);
                }
            }
        }

        return tempCSVFileNames;
    }

    // Finds all the trimmed CSV files and combines them into a single Instances object
    private void combineData() {
        List<String> csvFileNamesList = this.getCSVFiles();
        assert csvFileNamesList != null;
        this.csvFileNames = csvFileNamesList.toArray(new String[0]);

        // Sorted CSV file names
        Arrays.sort(this.csvFileNames);

        this.combinedGesturesData = null;
        for (int i = 0; i < this.csvFiles.size(); i++) {
            File file = this.csvFiles.get(i);
            Instances data = this.loadData(file, i);

            // this.addGestureId(data, i);
            this.csvFileNameToGestureId.put(this.csvFiles.get(i).getName(), i);

            if (this.combinedGesturesData == null) {
                this.combinedGesturesData = new Instances(data);
            } else {
                this.combinedGesturesData.addAll(data);
            }
        }
    }

    // Returns a new Instances object with only the specified columns
    private Instances getColumnData(Instances data, List<String> columns) {
        Instances newData = new Instances(data);
        newData.delete();

        // Create a mapping of attribute names to indices for faster lookup
        Map<String, Integer> attributeIndices = new HashMap<>();
        for (int i = 0; i < data.numAttributes(); i++) {
            Attribute attribute = data.attribute(i);
            attributeIndices.put(attribute.name(), i);
        }

        // Remove attributes not in the specified list
        for (int j = newData.numAttributes() - 1; j >= 0; j--) {
            Attribute attribute = newData.attribute(j);
            if (attribute != null && !columns.contains(attribute.name())) {
                newData.deleteAttributeAt(j);
            }
        }

        // Copy instances
        for (int j = 0; j < data.size(); j++) {
            Instance instance = data.get(j);
            Instance newDataInstance = new DenseInstance(newData.numAttributes());
            for (int k = 0; k < newData.numAttributes(); k++) {
                Attribute attr = newData.attribute(k);
                Integer indexInUnknownData = attributeIndices.get(attr.name());
                if (indexInUnknownData != null) {
                    newDataInstance.setValue(attr, instance.value(indexInUnknownData));
                }
            }
            newData.add(newDataInstance);
        }

        return newData;
    }

    public String[] getCSVFileNames() {
        Arrays.sort(this.csvFileNames);
        return this.csvFileNames;
    }

    // Perform KMeans clustering on the combined data and assign cluster IDs to instances
    public void getKMeans() throws Exception {
        combineData();

        for (int i = this.combinedGesturesData.numInstances() - 1; i >= 0; i--) {
            if (hasMissingValues(this.combinedGesturesData.instance(i))) {
                this.combinedGesturesData.delete(i);
            }
        }

        for (String prefix : this.prefixes) {
            List<String> columns = new ArrayList<>();

            columns.add(prefix + "_x");
            columns.add(prefix + "_y");
            columns.add(prefix + "_z");

            Instances kMeansColumns = getColumnData(this.combinedGesturesData, columns);

            SimpleKMeans kMeansModel = new SimpleKMeans();
            kMeansModel.setNumClusters(this.nClusters);
            kMeansModel.buildClusterer(kMeansColumns);

            Attribute clusterIdAttr = new Attribute("cluster_id_" + prefix);
            this.combinedGesturesData.insertAttributeAt(clusterIdAttr, this.combinedGesturesData.numAttributes());

            // Assign cluster IDs to instances in combinedDataGestures
            for (int i = 0; i < kMeansColumns.numInstances(); i++) {
                Instance instance = kMeansColumns.instance(i);
                int clusterId = kMeansModel.clusterInstance(instance);
                this.combinedGesturesData.instance(i).setValue(this.combinedGesturesData.numAttributes() - 1, clusterId);
            }

            kMeans.add(kMeansModel);
        }
    }

    public DenseInstance createInstance(double timestamp, double acc_x, double acc_y, double acc_z, double gyro_x, double gyro_y, double gyro_z) {
        DenseInstance instance = new DenseInstance(13);

        // Set attribute values
        instance.setValue(0, timestamp);
        instance.setValue(1, acc_x);
        instance.setValue(2, acc_y);
        instance.setValue(3, acc_z);
        instance.setValue(4, gyro_x);
        instance.setValue(5, gyro_y);
        instance.setValue(6, gyro_z);
        instance.setValue(7, acc_x);
        instance.setValue(8, acc_y);
        instance.setValue(9, acc_z);
        instance.setValue(10, acc_x);
        instance.setValue(11, acc_y);
        instance.setValue(12, acc_z);

        return instance;
    }

    public KMeansObj performKMeans(double timestamp, double acc_x, double acc_y, double acc_z, double gyro_x, double gyro_y, double gyro_z) {
        DenseInstance instance = createInstance(timestamp, acc_x, acc_y, acc_z, gyro_x, gyro_y, gyro_z);

        this.sensorInstances.add(instance);
        if (this.sensorInstances.size() == 100) {
            KMeansObj result = this.performKMeans(this.sensorInstances);
            this.sensorInstances.clear();
            return result;
        }
        return null;
    }

    private KMeansObj performKMeans(Instances unknownData) {
        try {
            double[] result = new double[this.csvFileNames.length];
            for (int i = 0; i < this.prefixes.length; i++) {
                String prefix = this.prefixes[i];
                List<String> columns = new ArrayList<>();

                columns.add(prefix + "_x");
                columns.add(prefix + "_y");
                columns.add(prefix + "_z");

                //Todo: Update the indexes for the new attributes
                if (Objects.equals(prefix, "acc_diff")) {
                    for (int j = 0; j < unknownData.numInstances() - 1; j++) {
                        unknownData.instance(j).setValue(unknownData.attribute("acc_diff_x"), unknownData.instance(j + 1).value(unknownData.attribute("acc_x")) - unknownData.instance(j).value(unknownData.attribute("acc_x")));
                        unknownData.instance(j).setValue(unknownData.attribute("acc_diff_y"), unknownData.instance(j + 1).value(unknownData.attribute("acc_y")) - unknownData.instance(j).value(unknownData.attribute("acc_y")));
                        unknownData.instance(j).setValue(unknownData.attribute("acc_diff_z"), unknownData.instance(j + 1).value(unknownData.attribute("acc_z")) - unknownData.instance(j).value(unknownData.attribute("acc_z")));
                    }
                }

                if (Objects.equals(prefix, "acc_ma")) {
                    for (int j = 0; j < unknownData.numInstances() - 1; j++) {
                        unknownData.instance(j).setValue(unknownData.attribute("acc_ma_x"), (unknownData.instance(j).value(unknownData.attribute("acc_x")) + unknownData.instance(j + 1).value(unknownData.attribute("acc_x"))) / 2);
                        unknownData.instance(j).setValue(unknownData.attribute("acc_ma_y"), (unknownData.instance(j).value(unknownData.attribute("acc_y")) + unknownData.instance(j + 1).value(unknownData.attribute("acc_y"))) / 2);
                        unknownData.instance(j).setValue(unknownData.attribute("acc_ma_z"), (unknownData.instance(j).value(unknownData.attribute("acc_z")) + unknownData.instance(j + 1).value(unknownData.attribute("acc_z"))) / 2);
                    }
                }

                Instances data = this.getColumnData(unknownData, columns);

                // Make a copy of data
                Instances dataCopy = new Instances(data);

                SimpleKMeans kMeansModel = this.kMeans.get(i);

                data.setClassIndex(data.numAttributes() - 1);

                Attribute clusterIdAttr = new Attribute("cluster_id_" + prefix);
                data.insertAttributeAt(clusterIdAttr, data.numAttributes());

                for (int j = 0; j < data.numInstances(); j++) {
                    int clusterId = kMeansModel.clusterInstance(dataCopy.instance(j));
                    data.instance(j).setValue(data.numAttributes() - 1, clusterId);
                }

                double[] resultCurr = this.processData(this.combinedGesturesData, data, prefix);
                for (int j = 0; j < result.length; j++) {
                    result[j] += this.weights[i] * resultCurr[j];
                }
            }

            for (int i = 0; i < result.length; i++) {
                result[i] = Math.round(result[i] * 100.0) / 100.0;
            }

            /*
            int maxIndex = 0;
            for (int i = 1; i < result.length; i++) {
                System.out.print(result[i] + " ");
                if (result[i] > result[maxIndex]) {
                    maxIndex = i;
                }
            }
            System.out.println();

            if (result[maxIndex] < 0.25) {
                return "Rest";
            }

            String resultFinal = this.csvFileNames[maxIndex];

             */

            int maxIndex = 0;
            for (int i = 1; i < result.length; i++) {
                if (result[i] > result[maxIndex]) {
                    maxIndex = i;
                }
            }

//            for (int i = 0; i < result.length; i++) {
//                System.out.print(this.csvFileNames[i] + "(" + result[i] + ") ");
//            }
//
//            System.out.println();

            String resultFinal = csvFileNames[maxIndex];
            double maxConfidence = result[maxIndex];

            String bucketPrefix = resultFinal.substring(0, resultFinal.lastIndexOf('_'));

            List<Double> bucketScores = new ArrayList<>();
            for (int i = 0; i < csvFileNames.length; i++) {
                if (csvFileNames[i].startsWith(bucketPrefix)) {
                    bucketScores.add(result[i]);
                }
            }

            double sum = 0;
            for (double score : bucketScores) {
                sum += score;
            }
            double averageConfidence = sum / bucketScores.size();

            double varianceSum = 0;
            for (double score : bucketScores) {
                varianceSum += Math.pow(score - averageConfidence, 2);
            }
            double standardDeviation = Math.sqrt(varianceSum / bucketScores.size());

            System.out.println("Bucket: " + bucketPrefix);
            System.out.println("Max Confidence: " + maxConfidence);
            System.out.println("Average Confidence: " + averageConfidence);
            System.out.println("Standard Deviation: " + standardDeviation);

            for (int i = 0; i < this.csvFileNames.length; i++) {
                System.out.println(this.csvFileNames[i] + ": " + result[i]);
            }

            System.out.println("\n\n");

            if (result[maxIndex] < 0.12 || averageConfidence < 0.12) {
                resultFinal = "Rest";
            } else {
                resultFinal = resultFinal.substring(0, resultFinal.length() - 4);
                resultFinal = resultFinal.replace("_", " ");
                resultFinal = resultFinal.replaceAll("[0-9]", "").trim();
                resultFinal = resultFinal.substring(0, 1).toUpperCase() + resultFinal.substring(1);
            }

            bucketPrefix = bucketPrefix.replace("_", " ");
            bucketPrefix = bucketPrefix.replaceAll("[0-9]", "").trim();
            bucketPrefix = bucketPrefix.substring(0, 1).toUpperCase() + bucketPrefix.substring(1);

            return new KMeansObj(resultFinal, bucketPrefix, maxConfidence, averageConfidence, standardDeviation);
        } catch (Exception e) {
            System.out.println("Error: " + e.getMessage());
            throw new RuntimeException(e);
        }
    }

    private double[] processData(Instances combinedData, Instances unknownData, String currPrefix) {
        int csvFilesLength = this.csvFileNames.length;
        double[] result = new double[csvFilesLength];

        for (int i = 0; i < csvFilesLength; i++) {
            List<Integer> cluster1 = new ArrayList<>();
            int gestureId = this.csvFileNameToGestureId.get(this.csvFileNames[i]);
            for (int j = 0; j < combinedData.numInstances(); j++) {
                int currGestureId = (int) combinedData.instance(j).value(combinedData.attribute("gesture_id"));
                if (gestureId == currGestureId && combinedData.attribute("cluster_id_" + currPrefix) != null) {
                    cluster1.add((int) combinedData.instance(j).value(combinedData.attribute("cluster_id_" + currPrefix)));
                }
            }

            List<Integer> cluster2 = new ArrayList<>();
            for (int j = 0; j < unknownData.numInstances(); j++) {
                cluster2.add((int) unknownData.instance(j).value(unknownData.attribute("cluster_id_" + currPrefix)));
            }

            result[i] = this.normSim(cluster1, cluster2);
        }

        return result;
    }

    private double[][] processDataForHeatmap(String currPrefix) {
        int csvFilesLength = this.csvFileNames.length;
        double[][] result = new double[csvFilesLength][csvFilesLength];

        for (int i = 0; i < csvFilesLength; i++) {
            for (int j = 0; j < csvFilesLength; j++) {
                List<Integer> cluster1 = new ArrayList<>();
                int gestureId = this.csvFileNameToGestureId.get(this.csvFileNames[i]);
                for (int k = 0; k < this.combinedGesturesData.numInstances(); k++) {
                    Attribute gestureIdAtt = this.combinedGesturesData.attribute("gesture_id");
                    Instance instance = this.combinedGesturesData.instance(k);
                    int currGestureId = (int) instance.value(gestureIdAtt);

                    if (gestureId == currGestureId) {
                        cluster1.add((int) this.combinedGesturesData.instance(k).value(this.combinedGesturesData.attribute("cluster_id_" + currPrefix)));
                    }
                }

                List<Integer> cluster2 = new ArrayList<>();
                gestureId = this.csvFileNameToGestureId.get(this.csvFileNames[j]);
                for (int k = 0; k < this.combinedGesturesData.numInstances(); k++) {
                    Attribute gestureIdAtt = this.combinedGesturesData.attribute("gesture_id");
                    Instance instance = this.combinedGesturesData.instance(k);
                    int currGestureId = (int) instance.value(gestureIdAtt);

                    if (gestureId == currGestureId) {
                        cluster2.add((int) this.combinedGesturesData.instance(k).value(this.combinedGesturesData.attribute("cluster_id_" + currPrefix)));
                    }
                }

                result[i][j] = this.normSim(cluster1, cluster2);
            }
        }
        return result;
    }

    public double[][] performKMeans() {
        double result[][] = new double[this.csvFileNames.length][this.csvFileNames.length];

        for (int i = 0; i < this.prefixes.length; i++) {
            String prefix = this.prefixes[i];
            List<String> columns = new ArrayList<>();

            columns.add(prefix + "_x");
            columns.add(prefix + "_y");
            columns.add(prefix + "_z");

            double[][] resultCurr = this.processDataForHeatmap(prefix);
            for (int j = 0; j < result.length; j++) {
                for (int k = 0; k < result[0].length; k++) {
                    result[j][k] += this.weights[i] * resultCurr[j][k];
                }
            }
        }

        for (int i = 0; i < result.length; i++) {
            for (int j = 0; j < result[0].length; j++) {
                result[i][j] = Math.round(result[i][j] * 100.0) / 100.0;
            }
        }

        return result;
    }

    private boolean hasMissingValues(Instance instance) {
        for (int i = 0; i < instance.numAttributes(); i++) {
            if (instance.isMissing(i)) {
                return true;
            }
        }
        return false;
    }

    private int longestCommonSubsequence(List<Integer> cluster1, List<Integer> cluster2) {
        int m = cluster1.size();
        int n = cluster2.size();
        int[][] dp = new int[m + 1][n + 1];

        for (int i = 1; i <= m; i++) {
            for (int j = 1; j <= n; j++) {
                if (cluster1.get(i - 1).equals(cluster2.get(j - 1))) {
                    dp[i][j] = dp[i - 1][j - 1] + 1;
                } else {
                    dp[i][j] = Math.max(dp[i - 1][j], dp[i][j - 1]);
                }
            }
        }

        return dp[m][n];
    }

    private double normSim(List<Integer> cluster1, List<Integer> cluster2) {
        double lcs = this.longestCommonSubsequence(cluster1, cluster2);
        return lcs / Math.max(cluster1.size(), cluster2.size());
    }
}
