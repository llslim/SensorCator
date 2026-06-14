using System;
using System.Collections.Generic;

namespace GestureFlowWPF.Models
{
    public class VocabularyCard
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string TextDisplay { get; set; } = string.Empty;
        public string AudioSpeechOutput { get; set; } = string.Empty; // Phonetic override or audio file path (.wav/.mp3)
        public string ImagePath { get; set; } = string.Empty; // Icon file path or fallback emoji
        public string AssociatedGestureName { get; set; } = string.Empty; // Mapped gesture template name
    }

    public class ActivityModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public List<Guid> CardIds { get; set; } = new List<Guid>();
    }

    public class PronunciationItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string OriginalWord { get; set; } = string.Empty;
        public string PhoneticSpeech { get; set; } = string.Empty;
    }

    public class AacSettings
    {
        public bool SpeechOn { get; set; } = true;
        public bool SensorInputActive { get; set; } = true;
        public bool FullScreenMode { get; set; } = false;

        // Speak Settings:
        // 0 = Speak word by word
        // 1 = Speak sentence
        // 2 = Speak full display
        public int SpeakSetting { get; set; } = 2; // Default to speak full display

        public string SpeakGestureName { get; set; } = string.Empty;
        public string ClearGestureName { get; set; } = string.Empty;

        // Speech Display style
        public string BgColor { get; set; } = "#1E1E2E"; // Default panel background
        public string FgColor { get; set; } = "#CDD6F4"; // Default text main color
        public double FontSize { get; set; } = 24.0;

        // TTS Settings
        public int TtsSpeed { get; set; } = 0; // -10 to 10
        public int TtsVolume { get; set; } = 100; // 0 to 100
        public string TtsVoiceName { get; set; } = string.Empty;
    }

    public class HeatmapCell
    {
        public string RowGesture { get; set; } = string.Empty;
        public string ColGesture { get; set; } = string.Empty;
        public double Distance { get; set; }
        public string DisplayValue => Distance == double.MaxValue || Distance < 0 ? "N/A" : Distance.ToString("F2");
        public string CellColor { get; set; } = "#252538"; // Hex color code for display
    }
}

