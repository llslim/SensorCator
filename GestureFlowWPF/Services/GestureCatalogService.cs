using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GestureFlowWPF.Models;

namespace GestureFlowWPF.Services
{
    public class GestureCatalogService
    {
        private readonly string _gesturesFolder;
        private readonly List<GestureTemplate> _templates = new List<GestureTemplate>();

        public IReadOnlyList<GestureTemplate> Templates => _templates;

        public GestureCatalogService()
        {
            string baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SensorCator");
            _gesturesFolder = Path.Combine(baseFolder, "gestures");

            if (!Directory.Exists(_gesturesFolder))
            {
                Directory.CreateDirectory(_gesturesFolder);
            }

            LoadAllTemplates();
        }

        public void LoadAllTemplates()
        {
            _templates.Clear();
            if (Directory.Exists(_gesturesFolder))
            {
                var files = Directory.GetFiles(_gesturesFolder, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var template = JsonSerializer.Deserialize<GestureTemplate>(json);
                        if (template != null)
                        {
                            _templates.Add(template);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading template {file}: {ex.Message}");
                    }
                }
            }
        }

        public void SaveTemplate(string name, List<double[]> points, double threshold, bool useGyro)
        {
            var template = new GestureTemplate
            {
                Name = name,
                Points = points,
                Threshold = threshold,
                UseGyro = useGyro
            };

            string fileName = $"{name.Replace(" ", "_").ToLower()}_template.json";
            string filePath = Path.Combine(_gesturesFolder, fileName);
            string json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            
            File.WriteAllText(filePath, json);
            _templates.Add(template);
        }

        public void DeleteTemplate(string name)
        {
            string fileName = $"{name.Replace(" ", "_").ToLower()}_template.json";
            string filePath = Path.Combine(_gesturesFolder, fileName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _templates.RemoveAll(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Matches the current rolling buffer of sensor values against all templates.
        /// Returns the name of the matched gesture, or null if no match is found.
        /// </summary>
        public GestureTemplate? MatchLiveGesture(List<double[]> liveBuffer)
        {
            if (liveBuffer.Count < 10) return null;

            GestureTemplate? bestMatch = null;
            double lowestDistance = double.MaxValue;

            foreach (var template in _templates)
            {
                if (template.Points.Count < 5) continue;

                // Match variations in execution speed by checking different window sizes
                // Check from 80% to 120% of template length
                int tempLen = template.Points.Count;
                int minWindow = (int)(tempLen * 0.8);
                int maxWindow = (int)(tempLen * 1.2);

                minWindow = Math.Max(10, minWindow);
                maxWindow = Math.Min(liveBuffer.Count, maxWindow);

                for (int w = minWindow; w <= maxWindow; w++)
                {
                    // Extract the last 'w' points from the live buffer
                    var subSequence = liveBuffer.GetRange(liveBuffer.Count - w, w);

                    // Filter points depending on if we use Accelerometer-only (3D) or Accel+Gyro (6D)
                    var filteredLive = FilterDimensions(subSequence, template.UseGyro);
                    var filteredTemplate = FilterDimensions(template.Points, template.UseGyro);

                    double dist = DtwMatcher.ComputeDtwDistance(filteredLive, filteredTemplate);

                    if (dist < template.Threshold && dist < lowestDistance)
                    {
                        lowestDistance = dist;
                        bestMatch = template;
                    }
                }
            }

            return bestMatch;
        }

        public List<double[]> FilterDimensions(List<double[]> rawPoints, bool useGyro)
        {
            var filtered = new List<double[]>(rawPoints.Count);
            foreach (var p in rawPoints)
            {
                if (useGyro)
                {
                    // Use both Accel (0,1,2) and Gyro (3,4,5)
                    if (p.Length >= 6)
                    {
                        filtered.Add(new double[] { p[0], p[1], p[2], p[3], p[4], p[5] });
                    }
                    else
                    {
                        filtered.Add(new double[] { p[0], p[1], p[2], 0, 0, 0 });
                    }
                }
                else
                {
                    // Use Accelerometer only (0,1,2)
                    filtered.Add(new double[] { p[0], p[1], p[2] });
                }
            }
            return filtered;
        }
    }
}
