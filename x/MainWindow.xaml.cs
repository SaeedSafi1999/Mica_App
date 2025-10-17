using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;

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
        private string clientId = Guid.NewGuid().ToString();

        // Online users
        private HashSet<string> onlineUsers = new HashSet<string>();

        public MainWindow()
        {
            InitializeComponent();
            ConnectWebSocket("ws://185.190.39.44:4040/chathub");
           // ConnectWebSocket("wss://localhost:7208/chathub");
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
            AddLog("Recording started");
        }

        private void StopRecording()
        {
            waveIn?.StopRecording();
            waveIn?.Dispose();
            waveIn = null;
            AddLog("Recording stopped");
        }

        private void StartPlayback()
        {
            waveProvider = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));
            waveOut = new WaveOutEvent();
            waveOut.Init(waveProvider);
            waveOut.Play();
            AddLog("Playback started");
        }

        private void StopPlayback()
        {
            waveOut?.Stop();
            waveOut?.Dispose();
            waveOut = null;
            AddLog("Playback stopped");
        }

        private void PlayAudio(byte[] buffer)
        {
            waveProvider?.AddSamples(buffer, 0, buffer.Length);
        }
        #endregion

        #region WebSocket Methods
        private async void ConnectWebSocket(string url)
        {
            try
            {
                ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(url), CancellationToken.None);
                AddLog("Connected to server");

                // Send SignalR Handshake
                var handshake = "{\"protocol\":\"json\",\"version\":1}\u001e";
                await SendMessageAsync(handshake);
                AddLog("Handshake sent");


                //Send your actual message
                var msg = "{\"type\":1,\"target\":\"SendMessage\",\"arguments\":[\"message\",\"Hello from Postman!\"]}\u001e";
                await SendMessageAsync(msg);
                AddLog("Message sent");

                // Start receiving loop
                _ = Task.Run(ReceiveLoop);
            }
            catch (Exception ex)
            {
                AddLog($"WebSocket connection error: {ex.Message}");
            }
        }


        private async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            while (ws.State == WebSocketState.Open)
            {
                try
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        HandleTextMessage(msg);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var audio = new byte[result.Count];
                        Array.Copy(buffer, audio, result.Count);
                        PlayAudio(audio);
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"WebSocket receive error: {ex.Message}");
                }
            }
        }


        public static string BuildInvocationMessage(string target, string[] arguments, string invocationId)
        {
            var payload = new
            {
                type = 1,
                target = target,
                arguments = arguments,
            };

            // Serialize to JSON and append U+001E (record separator)
            return JsonConvert.SerializeObject(payload) + "\u001e";
        }

        public class ChatMessage
        {
            public int? type { get; set; }
            public string? target { get; set; }
            public string[]? arguments { get; set; }
        }

        private void HandleTextMessage(string msg)
        {
            try
            {
                // پیام ممکنه شامل چند بخش جداشده با \u001e باشه
                var segments = msg.Split('\u001e', StringSplitOptions.RemoveEmptyEntries);

                foreach (var segment in segments)
                {
                    var data = JsonConvert.DeserializeObject<ChatMessage>(segment);

                    if (data == null || string.IsNullOrEmpty(data.target) || data.arguments == null || data.arguments.Length < 2)
                        continue;

                    if (data.type == 6)
                        continue; // پیام ping رو نادیده بگیر

                    if (data.target == "ReceiveMessage")
                    {
                        var senderId = data.arguments[0];
                        var message = data.arguments[1];

                        // نمایش پیام در UI
                        Dispatcher.BeginInvoke(() => AddMessage(senderId, message));
                        Dispatcher.BeginInvoke(() => AddLog($"Received from {senderId}: {message}"));

                        // فقط پیام‌های دیگران رو نمایش بده
                        if (senderId != clientId)
                        {
                            Dispatcher.BeginInvoke(() => AddMessage(senderId, message));
                            Dispatcher.BeginInvoke(() => AddLog($"Received from {senderId}: {message}"));
                        }

                        // اضافه کردن کاربر جدید به لیست آنلاین
                        if (!onlineUsers.Contains(senderId))
                        {
                            onlineUsers.Add(senderId);
                            Dispatcher.BeginInvoke(() => UsersList.Items.Add(senderId));
                            Dispatcher.BeginInvoke(() => AddLog($"User connected: {senderId}"));
                        }
                    }
                }
            }
            catch
            {
                Dispatcher.BeginInvoke(() => AddLog($"Invalid JSON received: {msg}"));
            }
        }

        private async Task SendMessageAsync(string msg)
        {
            if (ws?.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(msg);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

            }
        }

        private async Task SendAudioAsync(byte[] audio)
        {
            if (ws?.State == WebSocketState.Open)
            {
                await ws.SendAsync(new ArraySegment<byte>(audio), WebSocketMessageType.Binary, true, CancellationToken.None);
            }
        }
        #endregion

        #region UI Methods
        private void AddMessage(string sender, string message)
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
                Text = $"{sender}: {message}",
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            };

            border.Child = textBlock;
            ChatPanel.Children.Add(border);
            ChatScroll.ScrollToEnd();
        }

        private void AddLog(string text)
        {
            var tb = new TextBlock
            {
                Text = $"[{DateTime.Now:HH:mm:ss}] {text}",
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap
            };
            LogPanel.Children.Add(tb);
            (LogPanel.Parent as ScrollViewer)?.ScrollToEnd();
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            var msg = MessageInput.Text;
            if (string.IsNullOrEmpty(msg)) return;

            // Show message immediately
            this.AddMessage("You", msg);

            AddLog($"Sent: {msg}");

            // Send to server
            var mess = BuildInvocationMessage("SendMessage", new string[] { msg }, string.Empty);
            await SendMessageAsync(mess);

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
            StopPlayback();
        }
        #endregion
    }
}
