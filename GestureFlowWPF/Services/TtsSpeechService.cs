using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
using System.Linq;
using GestureFlowWPF.Models;

namespace GestureFlowWPF.Services
{
    public class TtsSpeechService : IDisposable
    {
        private SpeechSynthesizer? _synth;
        private string _originalText = string.Empty;
        private string _spokenText = string.Empty;
        private List<Tuple<int, int, int, int>> _indexMap = new List<Tuple<int, int, int, int>>(); // (origStart, origLen, spokenStart, spokenLen)

        public event EventHandler<WordSpokenEventArgs>? WordSpoken;

        public TtsSpeechService()
        {
            // Initialize SpeechSynthesizer on a background thread — first-time init
            // loads audio drivers and can block the UI thread for several seconds.
            Task.Run(() =>
            {
                _synth = new SpeechSynthesizer();
                _synth.SpeakProgress += Synth_SpeakProgress;
            });
        }

        public List<string> GetInstalledVoices()
        {
            try
            {
                return _synth?.GetInstalledVoices()
                             .Where(v => v.Enabled)
                             .Select(v => v.VoiceInfo.Name)
                             .ToList() ?? new List<string> { "Default System Voice" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting installed voices: {ex.Message}");
                return new List<string> { "Default System Voice" };
            }
        }

        public void Stop()
        {
            try
            {
                _synth?.SpeakAsyncCancelAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping speech: {ex.Message}");
            }
        }

        public void SpeakAsync(string text, AacSettings settings, List<PronunciationItem> dictionary)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            Stop();
            if (_synth == null) return; // TTS engine still initializing

            try
            {
                _synth.Rate = settings.TtsSpeed;
                _synth.Volume = settings.TtsVolume;

                if (!string.IsNullOrEmpty(settings.TtsVoiceName) && settings.TtsVoiceName != "Default System Voice")
                {
                    try
                    {
                        _synth.SelectVoice(settings.TtsVoiceName);
                    }
                    catch
                    {
                        // Fallback to default
                    }
                }

                _originalText = text;

                // Apply pronunciation dictionary replacements and build index mapping
                var (spokenText, map) = BuildSpokenAndMap(text, dictionary);
                _spokenText = spokenText;
                _indexMap = map;

                _synth.SpeakAsync(_spokenText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing speech async: {ex.Message}");
            }
        }

        private void Synth_SpeakProgress(object? sender, SpeakProgressEventArgs e)
        {
            int spokenPos = e.CharacterPosition;
            int spokenLen = e.CharacterCount;

            // Map spoken character position back to original text position
            int origPos = spokenPos;
            int origLen = spokenLen;

            var match = _indexMap.FirstOrDefault(m => spokenPos >= m.Item3 && spokenPos < m.Item3 + m.Item4);
            if (match != null)
            {
                // Simple proportional mapping
                double ratio = (double)(spokenPos - match.Item3) / match.Item4;
                origPos = match.Item1 + (int)(ratio * match.Item2);
                origLen = (int)((double)spokenLen / match.Item4 * match.Item2);
                origLen = Math.Max(1, origLen);
            }
            else
            {
                // Adjust if outside mapped segments
                // Find offset difference before this position
                int offset = 0;
                foreach (var m in _indexMap)
                {
                    if (spokenPos >= m.Item3 + m.Item4)
                    {
                        offset += (m.Item2 - m.Item4);
                    }
                }
                origPos = spokenPos + offset;
            }

            // Clamp values to original text length
            if (origPos < 0) origPos = 0;
            if (origPos + origLen > _originalText.Length) origLen = _originalText.Length - origPos;
            if (origLen < 0) origLen = 0;

            WordSpoken?.Invoke(this, new WordSpokenEventArgs(origPos, origLen, e.Text));
        }

        private (string spokenText, List<Tuple<int, int, int, int>> map) BuildSpokenAndMap(
            string text, List<PronunciationItem> dictionary)
        {
            if (dictionary == null || dictionary.Count == 0 || string.IsNullOrEmpty(text))
            {
                return (text, new List<Tuple<int, int, int, int>>());
            }

            // Sort dictionary by length descending to match longer phrases first
            var sortedDict = dictionary
                .Where(item => !string.IsNullOrWhiteSpace(item.OriginalWord))
                .OrderByDescending(item => item.OriginalWord.Length)
                .ToList();

            string spoken = "";
            var map = new List<Tuple<int, int, int, int>>();

            int srcIdx = 0;
            while (srcIdx < text.Length)
            {
                // Find if any dictionary item matches at srcIdx
                PronunciationItem? matchedItem = null;
                foreach (var item in sortedDict)
                {
                    if (srcIdx + item.OriginalWord.Length <= text.Length)
                    {
                        string sub = text.Substring(srcIdx, item.OriginalWord.Length);
                        if (sub.Equals(item.OriginalWord, StringComparison.OrdinalIgnoreCase))
                        {
                            // Verify word boundaries
                            bool isStartOk = srcIdx == 0 || !char.IsLetterOrDigit(text[srcIdx - 1]);
                            bool isEndOk = srcIdx + item.OriginalWord.Length == text.Length || !char.IsLetterOrDigit(text[srcIdx + item.OriginalWord.Length]);

                            if (isStartOk && isEndOk)
                            {
                                matchedItem = item;
                                break;
                            }
                        }
                    }
                }

                if (matchedItem != null)
                {
                    int origStart = srcIdx;
                    int origLen = matchedItem.OriginalWord.Length;
                    int spokenStart = spoken.Length;
                    int spokenLen = matchedItem.PhoneticSpeech.Length;

                    spoken += matchedItem.PhoneticSpeech;
                    map.Add(Tuple.Create(origStart, origLen, spokenStart, spokenLen));

                    srcIdx += origLen;
                }
                else
                {
                    spoken += text[srcIdx];
                    srcIdx++;
                }
            }

            return (spoken, map);
        }

        public void Dispose()
        {
            _synth?.Dispose();
        }
    }

    public class WordSpokenEventArgs : EventArgs
    {
        public int CharacterPosition { get; }
        public int CharacterCount { get; }
        public string Text { get; }

        public WordSpokenEventArgs(int charPos, int charCount, string text)
        {
            CharacterPosition = charPos;
            CharacterCount = charCount;
            Text = text;
        }
    }
}
