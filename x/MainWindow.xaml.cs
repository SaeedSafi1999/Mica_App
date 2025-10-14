using NAudio.Wave;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VoiceChatApp
{
    public partial class MainWindow : Window
    {
        // Audio
        private WaveInEvent waveIn;
        private WaveOutEvent waveOut;
        private BufferedWaveProvider waveProvider;

        // WebSocket
        private ClientWebSocket ws;

        public MainWindow()
        {
            InitializeComponent();
            ConnectWebSocket("ws://185.190.39.44:4040");
        }

        #region Audio Methods
        private void StartRecording()
        {
            waveIn = new WaveInEvent();
            waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
            waveIn.DataAvailable += async (s, a) =>
            {
                await SendAudioAsync(a.Buffer);
            };
            waveIn.StartRecording();
        }

        private void StopRecording()
        {
            waveIn?.StopRecording();
            waveIn?.Dispose();
        }

        private void StartPlayback()
        {
            waveProvider = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));
            waveOut = new WaveOutEvent();
            waveOut.Init(waveProvider);
            waveOut.Play();
        }

        private void PlayAudio(byte[] buffer)
        {
            waveProvider?.AddSamples(buffer, 0, buffer.Length);
        }
        #endregion

        #region WebSocket Methods
        private async void ConnectWebSocket(string url)
        {
            ws = new ClientWebSocket();
            await ws.ConnectAsync(new Uri(url), CancellationToken.None);

            _ = Task.Run(async () =>
            {
                var buffer = new byte[8192];
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Dispatcher.Invoke(() => AddChatMessage("Other", msg));
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var audio = new byte[result.Count];
                        Array.Copy(buffer, audio, result.Count);
                        PlayAudio(audio);
                    }
                }
            });
        }

        private async Task SendMessageAsync(string msg)
        {
            if (ws.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(msg);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private async Task SendAudioAsync(byte[] audio)
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.SendAsync(new ArraySegment<byte>(audio), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
        }
        #endregion

        #region UI Methods
        private void AddChatMessage(string sender, string message)
        {
            var border = new Border
            {
                Background = sender == "You" ? new SolidColorBrush(Color.FromRgb(40, 167, 69))
                                             : new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(5),
                MaxWidth = 400,
                HorizontalAlignment = sender == "You" ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = textBlock;
            ChatPanel.Children.Add(border);
            ChatScroll.ScrollToEnd();
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            var msg = MessageInput.Text;
            if (string.IsNullOrEmpty(msg)) return;

            await SendMessageAsync(msg);
            AddChatMessage("You", msg);
            MessageInput.Clear();
        }

        private void StartCall_Click(object sender, RoutedEventArgs e)
        {
            StartRecording();
            StartPlayback();
        }

        private void StopCall_Click(object sender, RoutedEventArgs e)
        {
            StopRecording();
        }
        #endregion
    }
}
