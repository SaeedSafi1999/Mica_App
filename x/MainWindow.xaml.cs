using Microsoft.AspNetCore.SignalR.Client;
using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YourNamespace;

namespace VoiceChatApp
{
    public partial class MainWindow : Window
    {
        // Audio
        private BufferedWaveProvider waveProvider;

        // WebSocket
        private readonly AudioSignalRClient _audio;
        private string clientId = Guid.NewGuid().ToString();

        // Online users
        private HashSet<string> onlineUsers = new HashSet<string>();

        public MainWindow()
        {
            InitializeComponent();
            _audio = new AudioSignalRClient(this,Dispatcher,clientId);
            ConnectWebSocket("ws://185.190.39.44:4040/chathub");
            //ConnectWebSocket("wss://localhost:7208/chathub");
        }

        #region Audio Methods




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
                await _audio.ConnectAsync(url);
                AddLog("Connected to server");
                // Start receiving loop
                _ = Task.Run(_audio.ReceiveLoop);
            }
            catch (Exception ex)
            {
                AddLog($"WebSocket connection error: {ex.Message}");
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


        private async Task SendMessageAsync(string msg)
        {
            await _audio.SendChatMessageAsync(msg);
        }

       
        #endregion

        #region UI Methods
        public void AddMessage(string sender, string message)
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

        public void AddLog(string text)
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
            await SendMessageAsync(msg);

            MessageInput.Clear();
        }

        private async void StartCall_Click(object sender, RoutedEventArgs e)
        {
            _audio.StartRecording();
            _audio. StartPlayback();
        }

        private void StopCall_Click(object sender, RoutedEventArgs e)
        {
            _audio.StopRecording();
            _audio.StopPlayback();
        }
        #endregion
    }
}
