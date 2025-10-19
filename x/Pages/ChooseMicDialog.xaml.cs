using NAudio.Wave;
using System.Windows;
using System.Windows.Controls;
using VoiceChatApp;

namespace x.Pages
{
    /// <summary>
    /// for choose microphone selection
    /// </summary>
    public partial class ChooseMicDialog : Window
    {
        private readonly AudioSignalRClient _SignalRService;
        private MainWindow _MainWindow;
        private readonly string _clientId = string.Empty;
        private readonly Action<int> StartRecordAction;
        private readonly Action PlayBackAction;
        public ChooseMicDialog(MainWindow mainWindow, Action<int> startRecord, Action playBackAction)
        {
            InitializeComponent();
            SetMicList();
            _MainWindow = mainWindow;
            StartRecordAction = startRecord;
            PlayBackAction = playBackAction;
        }

        private void SetMicList()
        {
            MicComboBox.Items.Clear();

            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var dev = WaveIn.GetCapabilities(i);

                MicComboBox.Items.Add(new ComboBoxItem
                {
                    Content = dev.ProductName,
                    Tag = i
                });

            }

            if (MicComboBox.Items.Count > 0)
                MicComboBox.SelectedIndex = 0;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ChooseAndCallBTN_Click(object sender, RoutedEventArgs e)
        {
            this.StartRecordAction.Invoke(1);
            this.PlayBackAction.Invoke();
            this.DialogResult = true;
            this.Close();
        }
    }
}
