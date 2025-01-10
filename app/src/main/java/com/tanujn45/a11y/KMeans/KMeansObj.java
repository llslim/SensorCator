package com.tanujn45.a11y.KMeans;

public class KMeansObj {
    String bucket;
    String res;
    double maxConfidence;
    double averageConf;
    double standardDeviation;

    public KMeansObj(String res, String bucket, double maxConfidence, double averageConf, double standardDeviation) {
        this.res = res;
        this.bucket = bucket;
        this.maxConfidence = maxConfidence;
        this.averageConf = averageConf;
        this.standardDeviation = standardDeviation;
    }

    public String getBucket() {
        return bucket;
    }

    public String getRes() {
        return res;
    }

    public double getMaxConfidence() {
        return roundToTwoDecimals(maxConfidence);
    }

    public double getAverageConf() {
        return roundToTwoDecimals(averageConf);
    }

    public double getStandardDeviation() {
        return roundToTwoDecimals(standardDeviation);
    }

    private double roundToTwoDecimals(double d) {
        return Math.round(d * 100.0) / 100.0;
    }
}
