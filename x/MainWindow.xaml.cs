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
            ConnectWebSocket("ws://185.190.39.44:4040/hub");
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

                // Send hello message with clientId
                var helloMsg = JsonConvert.SerializeObject(new { type = "hello", clientId = clientId });
                await SendMessageAsync(helloMsg);

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

        private void HandleTextMessage(string msg)
        {
            try
            {
                dynamic data = JsonConvert.DeserializeObject(msg);
                string type = data.type;

                if (type == "message")
                {
                    string senderId = data.clientId;
                    string messageText = data.message;

                    AddChatMessage(senderId, messageText);
                    AddLog($"Received from {senderId}: {messageText}");
                    // Only show messages from others
                    if (senderId != clientId)
                    {
                        Dispatcher.Invoke(() => AddChatMessage(senderId, messageText));
                        Dispatcher.Invoke(() => AddLog($"Received from {senderId}: {messageText}"));
                    }
                }
                else if (type == "hello")
                {
                    string newUserId = data.clientId;
                    if (!onlineUsers.Contains(newUserId))
                    {
                        onlineUsers.Add(newUserId);
                        Dispatcher.Invoke(() => UsersList.Items.Add(newUserId));
                        Dispatcher.Invoke(() => AddLog($"User connected: {newUserId}"));
                    }
                }
                else if (type == "disconnect")
                {
                    string userId = data.clientId;
                    if (onlineUsers.Contains(userId))
                    {
                        onlineUsers.Remove(userId);
                        Dispatcher.Invoke(() => UsersList.Items.Remove(userId));
                        Dispatcher.Invoke(() => AddLog($"User disconnected: {userId}"));
                    }
                }
                else
                {
                    Dispatcher.Invoke(() => AddLog($"Unknown message type: {msg}"));
                }
            }
            catch
            {
                Dispatcher.Invoke(() => AddLog($"Invalid JSON received: {msg}"));
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
            AddChatMessage("You", msg);
           
            AddLog($"Sent: {msg}");

            // Send to server
            var jsonMsg = JsonConvert.SerializeObject(new { type = "message", clientId = clientId, message = msg });
            await SendMessageAsync(jsonMsg);

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
