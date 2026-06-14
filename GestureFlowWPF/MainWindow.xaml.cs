using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using GestureFlowWPF.ViewModels;
using GestureFlowWPF.Models;

namespace GestureFlowWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel VM => (MainWindowViewModel)DataContext;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                DataContext = new MainWindowViewModel();
                Loaded += MainWindow_Loaded;
                Closed += (s, e) => VM.Cleanup();
                VM.WordSpoken += VM_WordSpoken;
            }
            catch (Exception ex)
            {
                string log = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "startup_crash.log");
                System.IO.File.WriteAllText(log,
                    $"[{DateTime.Now}] MainWindow ctor crash\n" +
                    $"Type: {ex.GetType().FullName}\n" +
                    $"Message: {ex.Message}\n" +
                    $"Stack:\n{ex.StackTrace}\n" +
                    (ex.InnerException != null
                        ? $"Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\nInnerStack:\n{ex.InnerException.StackTrace}\n"
                        : ""));
                MessageBox.Show(
                    $"Startup error — see startup_crash.log\n\n{ex.Message}",
                    "GestureFlow Startup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                VM.StartCamera();
            }
            catch (Exception ex)
            {
                string log = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "startup_crash.log");
                System.IO.File.AppendAllText(log,
                    $"[{DateTime.Now}] MainWindow_Loaded / StartCamera crash\n" +
                    $"Type: {ex.GetType().FullName}\nMessage: {ex.Message}\nStack:\n{ex.StackTrace}\n" +
                    (ex.InnerException != null ? $"Inner: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n" : "") +
                    new string('-', 60) + "\n");
                // Don't rethrow — keep app alive without camera
            }
        }

        private void TrimButton_Click(object sender, RoutedEventArgs e)
        {
            VM.TrimSelectedRun();
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            VM.ToggleScanning();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await VM.ToggleConnectionAsync();
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            VM.ToggleRecording();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            VM.RefreshLogFiles();
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            VM.ProcessSelectedCsv();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            VM.TogglePlayback();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TextBox textBox)
                {
                    var bindingExpression = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty);
                    bindingExpression?.UpdateSource();
                    Keyboard.ClearFocus();
                }
            }
        }

        private void RecordTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            VM.RecordGestureTemplate();
        }

        private void DeleteTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Models.GestureTemplate template)
            {
                VM.DeleteTemplate(template);
            }
        }

        // AAC Dashboard Event Handlers
        private void VM_WordSpoken(object? sender, Services.WordSpokenEventArgs e)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (VM.AacSettings.FullScreenMode)
                {
                    FullScreenTextBox.Select(e.CharacterPosition, e.CharacterCount);
                }
                else
                {
                    SpeechDisplayTextBox.Select(e.CharacterPosition, e.CharacterCount);
                }
            });
        }

        private void InsertTextAtCaret(string textToInsert)
        {
            TextBox activeTextBox = VM.AacSettings.FullScreenMode ? FullScreenTextBox : SpeechDisplayTextBox;
            activeTextBox.Focus();

            int caretIndex = activeTextBox.CaretIndex;
            string currentText = activeTextBox.Text;

            // Handle space padding
            string textWithSpace = textToInsert;
            if (caretIndex > 0 && !char.IsWhiteSpace(currentText[caretIndex - 1]) && !char.IsPunctuation(textToInsert[0]))
            {
                textWithSpace = " " + textToInsert;
            }

            activeTextBox.Text = currentText.Insert(caretIndex, textWithSpace);
            activeTextBox.CaretIndex = caretIndex + textWithSpace.Length;
            
            // Force binding source update
            var bindingExpression = BindingOperations.GetBindingExpression(activeTextBox, TextBox.TextProperty);
            bindingExpression?.UpdateSource();
        }

        private void VocabularyCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is VocabularyCard card)
            {
                InsertTextAtCaret(card.TextDisplay);
                VM.SpeakCardExplicit(card);
            }
        }

        private void KeyboardKey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Content is string keyText)
            {
                TextBox activeTextBox = VM.AacSettings.FullScreenMode ? FullScreenTextBox : SpeechDisplayTextBox;
                activeTextBox.Focus();

                int caretIndex = activeTextBox.CaretIndex;
                string currentText = activeTextBox.Text;

                if (keyText == "Space")
                {
                    activeTextBox.Text = currentText.Insert(caretIndex, " ");
                    activeTextBox.CaretIndex = caretIndex + 1;
                }
                else if (keyText == "Backspace")
                {
                    if (caretIndex > 0)
                    {
                        activeTextBox.Text = currentText.Remove(caretIndex - 1, 1);
                        activeTextBox.CaretIndex = caretIndex - 1;
                    }
                }
                else if (keyText == "Clear")
                {
                    activeTextBox.Text = "";
                    activeTextBox.CaretIndex = 0;
                }
                else
                {
                    activeTextBox.Text = currentText.Insert(caretIndex, keyText);
                    activeTextBox.CaretIndex = caretIndex + keyText.Length;
                }

                // Force binding source update
                var bindingExpression = BindingOperations.GetBindingExpression(activeTextBox, TextBox.TextProperty);
                bindingExpression?.UpdateSource();
            }
        }

        private void SpeakDisplay_Click(object sender, RoutedEventArgs e)
        {
            VM.TriggerSpeak();
        }

        private void ClearDisplay_Click(object sender, RoutedEventArgs e)
        {
            VM.TriggerClear();
        }

        private void SpeechToggle_Click(object sender, RoutedEventArgs e)
        {
            VM.ToggleSpeechOn();
        }

        private void SensorInputToggle_Click(object sender, RoutedEventArgs e)
        {
            VM.ToggleSensorInput();
        }

        private void FullScreenToggle_Click(object sender, RoutedEventArgs e)
        {
            VM.ToggleFullScreen();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            VM.OpenSettings();
        }

        private void CloseSettings_Click(object sender, RoutedEventArgs e)
        {
            VM.CloseSettings();
        }

        private void SetSettingsTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int tabIdx))
            {
                VM.SettingsTabIdx = tabIdx;
            }
        }

        private void AddCard_Click(object sender, RoutedEventArgs e)
        {
            VM.AddNewCard();
        }

        private void UpdateCard_Click(object sender, RoutedEventArgs e)
        {
            VM.UpdateSelectedCard();
        }

        private void CancelCardEdit_Click(object sender, RoutedEventArgs e)
        {
            VM.SelectedCardForEdit = null;
        }

        private void DeleteCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is VocabularyCard card)
            {
                VM.DeleteCard(card);
            }
        }

        private void AddActivity_Click(object sender, RoutedEventArgs e)
        {
            VM.AddNewActivity();
        }

        private void DeleteActivity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ActivityModel activity)
            {
                VM.DeleteActivity(activity);
            }
        }

        private void ToggleCardInActivity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkbox && checkbox.DataContext is VocabularyCard card)
            {
                VM.ToggleCardInActivity(card, VM.ActiveActivity!);
            }
        }

        private void AddDictionaryWord_Click(object sender, RoutedEventArgs e)
        {
            VM.AddPronunciationWord();
        }

        private void DeleteDictionaryWord_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is PronunciationItem item)
            {
                VM.DeletePronunciationWord(item);
            }
        }

        private void RecalculateHeatmap_Click(object sender, RoutedEventArgs e)
        {
            VM.CalculateAccuracyHeatmap();
        }
    }
}