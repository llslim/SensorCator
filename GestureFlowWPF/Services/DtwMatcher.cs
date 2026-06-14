using System;
using System.Collections.Generic;

namespace GestureFlowWPF.Services
{
    public static class DtwMatcher
    {
        /// <summary>
        /// Computes the normalized DTW distance between two multi-dimensional sequences.
        /// </summary>
        public static double ComputeDtwDistance(List<double[]> seq1, List<double[]> seq2)
        {
            if (seq1 == null || seq1.Count == 0 || seq2 == null || seq2.Count == 0)
                return double.MaxValue;

            // Z-score normalize the sequences to ensure scale and offset invariance
            var s1 = NormalizeSequence(seq1);
            var s2 = NormalizeSequence(seq2);

            int n = s1.Count;
            int m = s2.Count;

            // Dynamic Programming table
            double[,] dp = new double[n + 1, m + 1];

            // Initialize DP table boundary conditions
            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= m; j++)
                {
                    dp[i, j] = double.MaxValue;
                }
            }
            dp[0, 0] = 0;

            // Compute DTW warping path cost
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    double cost = EuclideanDistance(s1[i - 1], s2[j - 1]);
                    double minPrev = Math.Min(dp[i - 1, j], Math.Min(dp[i, j - 1], dp[i - 1, j - 1]));
                    
                    if (minPrev != double.MaxValue)
                    {
                        dp[i, j] = cost + minPrev;
                    }
                }
            }

            // Return path cost normalized by path length (n + m) to make it length-invariant
            return dp[n, m] / (n + m);
        }

        private static List<double[]> NormalizeSequence(List<double[]> seq)
        {
            int len = seq.Count;
            int dim = seq[0].Length;

            double[] means = new double[dim];
            double[] stds = new double[dim];

            // Compute mean for each dimension
            for (int d = 0; d < dim; d++)
            {
                double sum = 0;
                for (int i = 0; i < len; i++)
                {
                    sum += seq[i][d];
                }
                means[d] = sum / len;
            }

            // Compute standard deviation for each dimension
            for (int d = 0; d < dim; d++)
            {
                double varianceSum = 0;
                for (int i = 0; i < len; i++)
                {
                    double diff = seq[i][d] - means[d];
                    varianceSum += diff * diff;
                }
                double std = Math.Sqrt(varianceSum / len);
                stds[d] = std < 1e-6 ? 1.0 : std; // Prevent division by zero
            }

            // Z-score normalize
            List<double[]> normalized = new List<double[]>(len);
            for (int i = 0; i < len; i++)
            {
                double[] val = new double[dim];
                for (int d = 0; d < dim; d++)
                {
                    val[d] = (seq[i][d] - means[d]) / stds[d];
                }
                normalized.Add(val);
            }

            return normalized;
        }

        private static double EuclideanDistance(double[] p1, double[] p2)
        {
            int dim = Math.Min(p1.Length, p2.Length);
            double sum = 0;
            for (int i = 0; i < dim; i++)
            {
                double diff = p1[i] - p2[i];
                sum += diff * diff;
            }
            return Math.Sqrt(sum);
        }
    }
}
