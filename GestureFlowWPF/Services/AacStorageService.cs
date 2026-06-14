using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using GestureFlowWPF.Models;

namespace GestureFlowWPF.Services
{
    public class AacStorageService
    {
        private readonly string _aacFolder;
        private readonly string _settingsFile;
        private readonly string _cardsFile;
        private readonly string _activitiesFile;
        private readonly string _dictionaryFile;

        public AacStorageService()
        {
            string baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SensorCator");
            _aacFolder = Path.Combine(baseFolder, "aac");
            _settingsFile = Path.Combine(_aacFolder, "settings.json");
            _cardsFile = Path.Combine(_aacFolder, "cards.json");
            _activitiesFile = Path.Combine(_aacFolder, "activities.json");
            _dictionaryFile = Path.Combine(_aacFolder, "dictionary.json");

            if (!Directory.Exists(_aacFolder))
            {
                Directory.CreateDirectory(_aacFolder);
            }
        }

        public AacSettings LoadSettings()
        {
            if (File.Exists(_settingsFile))
            {
                try
                {
                    string json = File.ReadAllText(_settingsFile);
                    return JsonSerializer.Deserialize<AacSettings>(json) ?? new AacSettings();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                    return new AacSettings();
                }
            }
            var defaults = new AacSettings();
            SaveSettings(defaults);
            return defaults;
        }

        public void SaveSettings(AacSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public List<VocabularyCard> LoadCards()
        {
            if (File.Exists(_cardsFile))
            {
                try
                {
                    string json = File.ReadAllText(_cardsFile);
                    return JsonSerializer.Deserialize<List<VocabularyCard>>(json) ?? new List<VocabularyCard>();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading cards: {ex.Message}");
                }
            }

            var defaultCards = GetDefaultCards();
            SaveCards(defaultCards);
            return defaultCards;
        }

        public void SaveCards(List<VocabularyCard> cards)
        {
            try
            {
                string json = JsonSerializer.Serialize(cards, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_cardsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving cards: {ex.Message}");
            }
        }

        public List<ActivityModel> LoadActivities(List<VocabularyCard> allCards)
        {
            if (File.Exists(_activitiesFile))
            {
                try
                {
                    string json = File.ReadAllText(_activitiesFile);
                    return JsonSerializer.Deserialize<List<ActivityModel>>(json) ?? new List<ActivityModel>();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading activities: {ex.Message}");
                }
            }

            var defaultActivities = GetDefaultActivities(allCards);
            SaveActivities(defaultActivities);
            return defaultActivities;
        }

        public void SaveActivities(List<ActivityModel> activities)
        {
            try
            {
                string json = JsonSerializer.Serialize(activities, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_activitiesFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving activities: {ex.Message}");
            }
        }

        public List<PronunciationItem> LoadDictionary()
        {
            if (File.Exists(_dictionaryFile))
            {
                try
                {
                    string json = File.ReadAllText(_dictionaryFile);
                    return JsonSerializer.Deserialize<List<PronunciationItem>>(json) ?? new List<PronunciationItem>();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading dictionary: {ex.Message}");
                }
            }
            var empty = new List<PronunciationItem>();
            SaveDictionary(empty);
            return empty;
        }

        public void SaveDictionary(List<PronunciationItem> dictionary)
        {
            try
            {
                string json = JsonSerializer.Serialize(dictionary, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dictionaryFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving dictionary: {ex.Message}");
            }
        }

        private List<VocabularyCard> GetDefaultCards()
        {
            return new List<VocabularyCard>
            {
                new VocabularyCard { TextDisplay = "Hello", ImagePath = "👋", AssociatedGestureName = "" },
                new VocabularyCard { TextDisplay = "Thank you", ImagePath = "🙏", AssociatedGestureName = "" },
                new VocabularyCard { TextDisplay = "Yes", ImagePath = "✅", AssociatedGestureName = "" },
                new VocabularyCard { TextDisplay = "No", ImagePath = "❌", AssociatedGestureName = "" },
                new VocabularyCard { TextDisplay = "I need water", ImagePath = "🥛", AssociatedGestureName = "" },
                new VocabularyCard { TextDisplay = "I am hungry", ImagePath = "🍎", AssociatedGestureName = "" },
                new VocabularyCard { TextDisplay = "Help", ImagePath = "🆘", AssociatedGestureName = "" },
                new VocabularyCard { TextDisplay = "Go", ImagePath = "🚶", AssociatedGestureName = "" },
                new VocabularyCard { TextDisplay = "Stop", ImagePath = "🛑", AssociatedGestureName = "" }
            };
        }

        private List<ActivityModel> GetDefaultActivities(List<VocabularyCard> allCards)
        {
            var generalCards = allCards.Where(c => c.TextDisplay == "Hello" || c.TextDisplay == "Thank you" || c.TextDisplay == "Yes" || c.TextDisplay == "No").Select(c => c.Id).ToList();
            var needsCards = allCards.Where(c => c.TextDisplay == "I need water" || c.TextDisplay == "I am hungry" || c.TextDisplay == "Help").Select(c => c.Id).ToList();
            var actionsCards = allCards.Where(c => c.TextDisplay == "Go" || c.TextDisplay == "Stop").Select(c => c.Id).ToList();

            return new List<ActivityModel>
            {
                new ActivityModel { Name = "General", CardIds = generalCards },
                new ActivityModel { Name = "Needs", CardIds = needsCards },
                new ActivityModel { Name = "Actions", CardIds = actionsCards }
            };
        }
    }
}
