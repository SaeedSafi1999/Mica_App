using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualBasic.Devices;
using NAudio.Wave;
using Newtonsoft.Json;
using System.Security.Policy;
using System.Text;
using System.Windows.Threading;
using VoiceChatApp;
using YourNamespace;
using static VoiceChatApp.MainWindow;

public class AudioSignalRClient
{
    private HubConnection connection;
    private WaveInEvent waveIn;
    private WaveOutEvent waveOut;
    private BufferedWaveProvider playbackBuffer;
    private MainWindow _mainWindow;
    private readonly Dispatcher _dispatcher;
    private readonly string _clientId;
    private string _url;


    public AudioSignalRClient(MainWindow mainWindow, Dispatcher dispatcher, string clientId)
    {
        _mainWindow = mainWindow;
        _dispatcher = dispatcher;
        _clientId = clientId;
    }

    public async Task ConnectAsync(string url)
    {
        _url = url;
        await StartAsync(url);
    }

    public async Task StartAsync(string url)
    {
        connection = new HubConnectionBuilder()
          .WithUrl(url)
          .WithAutomaticReconnect()
          .Build();

        connection.On<string, string>("ReceiveMessage", (clientId, message) =>
        {
            Console.WriteLine($"Received message from {clientId} - {message}");
            _dispatcher.BeginInvoke(() => _mainWindow.AddMessage(clientId, message));
            _dispatcher.BeginInvoke(() => _mainWindow.AddLog($"Received from {clientId}: {message}"));
            WindowsNotifier.ShowNotification($"new Message from {clientId}", message);
        });

        connection.On<string, byte[]>("ReceiveAudio", (clientId, audioData) =>
        {
            Console.WriteLine($"Received audio from {clientId} - {audioData.Length} bytes");
            playbackBuffer?.AddSamples(audioData, 0, audioData.Length);
        });

        await connection.StartAsync();
        Console.WriteLine("Connected to SignalR Hub");


    }

    private void HandleTextMessage(string msg, string mainClientId)
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

                    if (senderId != mainClientId)
                    {

                    }


                }
            }
        }
        catch
        {
            _dispatcher.BeginInvoke(() => _mainWindow.AddLog($"Invalid JSON received: {msg}"));
        }
    }

    public void StartRecording()
    {
        waveIn = new WaveInEvent();
        waveIn.WaveFormat = new WaveFormat(16000, 16, 1);

        waveIn.DataAvailable += async (s, a) =>
        {
            await SendAudioAsync(a.Buffer);
        };

        waveIn.StartRecording();
        Console.WriteLine("Recording started");
    }

    public void StopRecording()
    {
        if (waveIn != null)
        {
            waveIn.StopRecording();
            waveIn.Dispose();
            waveIn = null;
            Console.WriteLine("Recording stopped");
        }
    }

    public HubConnectionState GetConnectionState()
        => connection.State;

    private async Task SendAudioAsync(byte[] audio)
    {
        if (connection.State == HubConnectionState.Connected)
        {
            await connection.InvokeAsync("SendAudio", audio);
        }
    }

    public void StartPlayback()
    {
        if (waveOut == null)
        {
            playbackBuffer = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));
            waveOut = new WaveOutEvent();
            waveOut.Init(playbackBuffer);
            waveOut.Play();
            Console.WriteLine("Playback started");
        }

    }

    //public void StartPlayback()
    //{
    //    if (waveOut == null)
    //    {
    //        playbackBuffer = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));

    //        waveOut = new WaveOutEvent
    //        {
    //            DeviceNumber = GetVirtualCableDeviceIndex()
    //        };

    //        waveOut.Init(playbackBuffer);
    //        waveOut.Play();
    //        Console.WriteLine("Playback started");
    //    }
    //}

    public void StopPlayback()
    {
        if (waveOut != null)
        {
            waveOut.Stop();
            waveOut.Dispose();
            waveOut = null;
            playbackBuffer = null;
            Console.WriteLine("Playback stopped");
        }
    }

    public async Task SendChatMessageAsync(string message)
    {
        if (connection.State == HubConnectionState.Connected)
        {
            var payload = new
            {
                type = 1,
                target = "SendMessage",
                arguments = new[] { message }
            };

            string json = JsonConvert.SerializeObject(payload) + "\u001e"; // مهم: جداکننده پیام
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            await connection.SendAsync("SendMessage", message);
        }
    }


    private int GetVirtualCableDeviceIndex()
    {
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            if (caps.ProductName.Contains("VB-Audio") || caps.ProductName.Contains("Virtual Cable"))
            {
                return i;
            }
        }
        return 0; // fallback به دستگاه پیش‌فرض
    }


    public async Task ReceiveLoop()
    {
        while (true)
        {
            try
            {
                if (GetConnectionState() != HubConnectionState.Connected)
                {
                    _mainWindow.AddLog("Disconnected from SignalR Hub. Attempting to reconnect...");
                    await StartAsync(_url);
                    _mainWindow.AddLog("Reconnected to SignalR Hub.");
                }

                await Task.Delay(3000);
            }
            catch (Exception ex)
            {
                _mainWindow.AddLog($"SignalR receive loop error: {ex.Message}");
                await Task.Delay(5000);
            }
        }
    }
}


//using Microsoft.AspNetCore.SignalR.Client;
//using Microsoft.VisualBasic.Devices;
//using NAudio.Wave;
//using Newtonsoft.Json;
//using System;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Windows.Threading;
//using VoiceChatApp;
//using YourNamespace;
//using static VoiceChatApp.MainWindow;

//public class AudioSignalRClient : IDisposable
//{
//    private HubConnection connection;
//    private WaveInEvent waveIn;
//    // keep these fields for compatibility with your original layout, but playback is per-client
//    private WaveOutEvent waveOut;
//    private BufferedWaveProvider playbackBuffer;

//    private MainWindow _mainWindow;
//    private readonly Dispatcher _dispatcher;
//    private readonly string _clientId;
//    private string _url;

//    // Per-client playback containers
//    // value: (buffer, output, lastSeenUtc)
//    private readonly Dictionary<string, (BufferedWaveProvider buffer, WaveOutEvent output, DateTime lastSeen)> _playbacks
//        = new Dictionary<string, (BufferedWaveProvider, WaveOutEvent, DateTime)>();
//    private readonly object _playbacksLock = new object();

//    private CancellationTokenSource _cleanupCts;
//    private Task _cleanupTask;

//    // settings
//    private readonly int _sampleRate = 16000;
//    private readonly int _channels = 1;
//    private readonly int _bits = 16;
//    private readonly int _maxBufferMs = 250; // keep low for low latency
//    private readonly int _playbackIdleSeconds = 30; // release playback if no data for N seconds

//    public AudioSignalRClient(MainWindow mainWindow, Dispatcher dispatcher, string clientId)
//    {
//        _mainWindow = mainWindow;
//        _dispatcher = dispatcher;
//        _clientId = clientId;
//    }

//    public async Task ConnectAsync(string url)
//    {
//        _url = url;
//        await StartAsync(url);
//    }

//    public async Task StartAsync(string url)
//    {
//        if (connection != null && connection.State == HubConnectionState.Connected)
//            return;

//        connection = new HubConnectionBuilder()
//          .WithUrl(url)
//          .WithAutomaticReconnect()
//          .Build();

//        // text messages
//        connection.On<string, string>("ReceiveMessage", (clientId, message) =>
//        {
//            try
//            {
//                Console.WriteLine($"Received message from {clientId} - {message}");
//                _dispatcher.BeginInvoke(() => _mainWindow.AddMessage(clientId, message));
//                _dispatcher.BeginInvoke(() => _mainWindow.AddLog($"Received from {clientId}: {message}"));
//                WindowsNotifier.ShowNotification($"new Message from {clientId}", message);
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"ReceiveMessage handler error: {ex.Message}");
//            }
//        });

//        // audio: per-client handling
//        connection.On<string, byte[]>("ReceiveAudio", (clientId, audioData) =>
//        {
//            try
//            {
//                if (clientId == _clientId)
//                {
//                    // ignore our own audio if server echoes it back
//                    return;
//                }

//                if (audioData == null || audioData.Length == 0) return;

//                lock (_playbacksLock)
//                {
//                    if (!_playbacks.TryGetValue(clientId, out var entry))
//                    {
//                        var fmt = new WaveFormat(_sampleRate, _bits, _channels);
//                        var buf = new BufferedWaveProvider(fmt)
//                        {
//                            BufferDuration = TimeSpan.FromMilliseconds(_maxBufferMs),
//                            DiscardOnBufferOverflow = true
//                        };

//                        var outDev = new WaveOutEvent();
//                        outDev.Init(buf);
//                        outDev.Play();

//                        _playbacks[clientId] = (buf, outDev, DateTime.UtcNow);
//                        entry = _playbacks[clientId];

//                        // log creation
//                        _dispatcher.BeginInvoke(() => _mainWindow.AddLog($"Created playback for {clientId}"));
//                    }
//                    else
//                    {
//                        // update lastSeen
//                        _playbacks[clientId] = (entry.buffer, entry.output, DateTime.UtcNow);
//                    }

//                    try
//                    {
//                        entry.buffer.AddSamples(audioData, 0, audioData.Length);
//                    }
//                    catch (Exception ex)
//                    {
//                        Console.WriteLine($"AddSamples error for {clientId}: {ex.Message}");
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"ReceiveAudio handler error: {ex.Message}");
//            }
//        });

//        // start connection
//        await connection.StartAsync();
//        Console.WriteLine("Connected to SignalR Hub");
//        _dispatcher.BeginInvoke(() => _mainWindow.AddLog("Connected to SignalR Hub"));

//        // Start cleanup background task
//        StartPlaybackCleanupTask();
//    }

//    private void StartPlaybackCleanupTask()
//    {
//        if (_cleanupCts != null) return;

//        _cleanupCts = new CancellationTokenSource();
//        var ct = _cleanupCts.Token;
//        _cleanupTask = Task.Run(async () =>
//        {
//            while (!ct.IsCancellationRequested)
//            {
//                try
//                {
//                    List<string> toRemove = null;
//                    var now = DateTime.UtcNow;

//                    lock (_playbacksLock)
//                    {
//                        foreach (var kv in _playbacks)
//                        {
//                            var clientId = kv.Key;
//                            var lastSeen = kv.Value.lastSeen;
//                            if ((now - lastSeen).TotalSeconds > _playbackIdleSeconds)
//                            {
//                                if (toRemove == null) toRemove = new List<string>();
//                                toRemove.Add(clientId);
//                            }
//                        }

//                        if (toRemove != null)
//                        {
//                            foreach (var id in toRemove)
//                            {
//                                if (_playbacks.TryGetValue(id, out var entry))
//                                {
//                                    try
//                                    {
//                                        entry.output?.Stop();
//                                        entry.output?.Dispose();
//                                    }
//                                    catch { }
//                                }
//                                _playbacks.Remove(id);
//                                _dispatcher.BeginInvoke(() => _mainWindow.AddLog($"Released playback for {id} due to inactivity."));
//                            }
//                        }
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"Cleanup task error: {ex.Message}");
//                }

//                try { await Task.Delay(5000, ct); } catch { break; }
//            }
//        }, ct);
//    }

//    private void StopPlaybackCleanupTask()
//    {
//        try
//        {
//            _cleanupCts?.Cancel();
//            _cleanupTask = null;
//            _cleanupCts?.Dispose();
//            _cleanupCts = null;
//        }
//        catch { }
//    }

//    private void HandleTextMessage(string msg, string mainClientId)
//    {
//        try
//        {
//            var segments = msg.Split('\u001e', StringSplitOptions.RemoveEmptyEntries);

//            foreach (var segment in segments)
//            {
//                var data = JsonConvert.DeserializeObject<ChatMessage>(segment);

//                if (data == null || string.IsNullOrEmpty(data.target) || data.arguments == null || data.arguments.Length < 2)
//                    continue;

//                if (data.type == 6)
//                    continue; // ping

//                if (data.target == "ReceiveMessage")
//                {
//                    var senderId = data.arguments[0];
//                    var message = data.arguments[1];

//                    if (senderId != mainClientId)
//                    {
//                        // optionally handle
//                    }
//                }
//            }
//        }
//        catch
//        {
//            _dispatcher.BeginInvoke(() => _mainWindow.AddLog($"Invalid JSON received: {msg}"));
//        }
//    }

//    public void StartRecording()
//    {
//        if (waveIn != null) return;

//        waveIn = new WaveInEvent()
//        {
//            WaveFormat = new WaveFormat(_sampleRate, _bits, _channels)
//        };

//        // IMPORTANT: use BytesRecorded and avoid awaiting inside the event
//        waveIn.DataAvailable += (s, a) =>
//        {
//            var length = a.BytesRecorded;
//            if (length <= 0) return;

//            var dataCopy = new byte[length];
//            Array.Copy(a.Buffer, 0, dataCopy, 0, length);

//            // fire-and-forget to avoid blocking audio thread
//            _ = Task.Run(async () =>
//            {
//                try
//                {
//                    await SendAudioAsync(dataCopy, length);
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"SendAudioAsync task error: {ex.Message}");
//                }
//            });
//        };

//        waveIn.StartRecording();
//        Console.WriteLine("Recording started");
//        _dispatcher.BeginInvoke(() => _mainWindow.AddLog("Recording started"));
//    }

//    public void StopRecording()
//    {
//        if (waveIn != null)
//        {
//            try
//            {
//                waveIn.StopRecording();
//                waveIn.Dispose();
//            }
//            catch { }
//            finally
//            {
//                waveIn = null;
//            }
//            Console.WriteLine("Recording stopped");
//            _dispatcher.BeginInvoke(() => _mainWindow.AddLog("Recording stopped"));
//        }
//    }

//    public HubConnectionState GetConnectionState()
//        => connection?.State ?? HubConnectionState.Disconnected;

//    private async Task SendAudioAsync(byte[] audio, int length)
//    {
//        if (connection == null) return;

//        try
//        {
//            if (connection.State == HubConnectionState.Connected)
//            {
//                if (length != audio.Length)
//                {
//                    var tmp = new byte[length];
//                    Array.Copy(audio, 0, tmp, 0, length);
//                    await connection.InvokeAsync("SendAudio", tmp);
//                }
//                else
//                {
//                    await connection.InvokeAsync("SendAudio", audio);
//                }
//            }
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"SendAudioAsync error: {ex.Message}");
//        }
//    }

//    // keep StartPlayback for compatibility (not used because playback is per-client)
//    public void StartPlayback()
//    {
//        // no-op (playback created per remote client when data arrives)
//    }

//    public void StopPlayback()
//    {
//        lock (_playbacksLock)
//        {
//            foreach (var kv in _playbacks)
//            {
//                try
//                {
//                    kv.Value.output?.Stop();
//                    kv.Value.output?.Dispose();
//                }
//                catch { }
//            }
//            _playbacks.Clear();
//        }

//        // also dispose single fallback playback if any
//        try
//        {
//            playbackBuffer = null;
//            waveOut?.Stop();
//            waveOut?.Dispose();
//            waveOut = null;
//        }
//        catch { }

//        _dispatcher.BeginInvoke(() => _mainWindow.AddLog("Stopped all playback"));
//    }

//    public async Task SendChatMessageAsync(string message)
//    {
//        if (connection == null) return;

//        try
//        {
//            if (connection.State == HubConnectionState.Connected)
//            {
//                await connection.SendAsync("SendMessage", message);
//            }
//        }
//        catch (Exception ex)
//        {
//            _dispatcher.BeginInvoke(() => _mainWindow.AddLog($"SendChatMessageAsync error: {ex.Message}"));
//        }
//    }

//    private int GetVirtualCableDeviceIndex()
//    {
//        for (int i = 0; i < WaveOut.DeviceCount; i++)
//        {
//            var caps = WaveOut.GetCapabilities(i);
//            if (caps.ProductName.Contains("VB-Audio") || caps.ProductName.Contains("Virtual Cable"))
//            {
//                return i;
//            }
//        }
//        return 0; // fallback to default
//    }

//    public async Task ReceiveLoop()
//    {
//        while (true)
//        {
//            try
//            {
//                if (GetConnectionState() != HubConnectionState.Connected)
//                {
//                    _dispatcher.BeginInvoke(() => _mainWindow.AddLog("Disconnected from SignalR Hub. Attempting to reconnect..."));
//                    try
//                    {
//                        await StartAsync(_url);
//                        _dispatcher.BeginInvoke(() => _mainWindow.AddLog("Reconnected to SignalR Hub."));
//                    }
//                    catch (Exception ex)
//                    {
//                        _dispatcher.BeginInvoke(() => _mainWindow.AddLog($"Reconnect failed: {ex.Message}"));
//                    }
//                }

//                await Task.Delay(3000);
//            }
//            catch (Exception ex)
//            {
//                _dispatcher.BeginInvoke(() => _mainWindow.AddLog($"SignalR receive loop error: {ex.Message}"));
//                await Task.Delay(5000);
//            }
//        }
//    }

//    public void StopAll()
//    {
//        StopRecording();
//        StopPlayback();
//        StopPlaybackCleanupTask();

//        try
//        {
//            if (connection != null)
//            {
//                _ = connection.StopAsync();
//                _ = connection.DisposeAsync();
//            }
//        }
//        catch { }

//        connection = null;
//    }

//    public void Dispose()
//    {
//        StopAll();
//    }
//}
