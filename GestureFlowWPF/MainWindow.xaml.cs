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
            InitializeComponent();
            DataContext = new MainWindowViewModel();
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
    }
}