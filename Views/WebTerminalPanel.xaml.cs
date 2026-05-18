using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using ClaudeSessionManager.Wpf.ConPTY;

namespace ClaudeSessionManager.Wpf.Views;

public partial class WebTerminalPanel : UserControl
{
    private const int OutputBufferSize = 4096;
    private const int ShutdownJoinTimeoutMs = 2000;
    private const int InitialCdDelayMs = 100;
    private const int StartupCommandDelayMs = 300;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string? LastCwd { get; private set; }
    public string? InitialCwd { get; set; }
    public string? RunOnStart { get; set; }
    public int PanelIndex { get; set; } = -1;

    private Terminal? _terminal;
    private CancellationTokenSource? _cts;
    private Thread? _terminalThread;
    private bool _coreWebView2Ready;

    public WebTerminalPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        GotFocus += (s, e) => App.SetActivePanel(this);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_terminal != null) return;
        await InitializeWebViewAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 분할 detach는 cleanup 트리거 아님. CloseTab → Shutdown 명시 호출.
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            await WebView.EnsureCoreWebView2Async(App.SharedWebView2Env);
            _coreWebView2Ready = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebTerminalPanel] EnsureCoreWebView2Async failed: {ex.Message}");
            return;
        }

        var t = App.Settings.Terminal;
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Terminal");
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            t.VirtualHost, assetsDir, CoreWebView2HostResourceAccessKind.Allow);
        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        WebView.GotFocus += (s, e) => App.SetActivePanel(this);
        WebView.CoreWebView2.Navigate($"https://{t.VirtualHost}/index.html");
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string raw;
        try { raw = e.TryGetWebMessageAsString(); }
        catch { return; }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            var type = typeEl.GetString();

            switch (type)
            {
                case "ready":
                    DiagLog("WebMsg ready");
                    SendConfig();
                    StartTerminal();
                    break;
                case "input":
                    if (root.TryGetProperty("data", out var dataEl))
                    {
                        var input = dataEl.GetString();
                        DiagLog($"WebMsg input len={input?.Length ?? 0}");
                        if (!string.IsNullOrEmpty(input)) _terminal?.WriteToPseudoConsole(input);
                    }
                    break;
                case "cwd":
                    if (root.TryGetProperty("path", out var pathEl)) {
                        var path = pathEl.GetString();
                        DiagLog($"WebMsg cwd path={path}");
                        if (!string.IsNullOrWhiteSpace(path) && path != LastCwd)
                        {
                            LastCwd = path;
                            App.NotifyCwdChanged(this);
                        }
                    }
                    break;
                case "focus":
                    App.SetActivePanel(this);
                    break;
                case "resize":
                    DiagLog("WebMsg resize");
                    if (root.TryGetProperty("cols", out var colsEl) && root.TryGetProperty("rows", out var rowsEl))
                    {
                        int cols = colsEl.GetInt32();
                        int rows = rowsEl.GetInt32();
                        if (cols > 0 && rows > 0)
                        {
                            try { _terminal?.Resize(cols, rows); }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Resize failed: {ex.Message}"); }
                        }
                    }
                    break;
                case "debug":
                    var dmsg = root.TryGetProperty("msg", out var msgEl) ? msgEl.GetString() : "";
                    var dsample = root.TryGetProperty("sample", out var sEl) ? sEl.GetString() : "";
                    var dpayload = root.TryGetProperty("payload", out var pEl) ? pEl.GetString() : "";
                    DiagLog($"WebMsg debug msg={dmsg} payload={dpayload} sample={dsample}");
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine($"[WebTerminalPanel] unknown message type: {type}");
                    break;
            }
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebTerminalPanel] JSON parse failed: {ex.Message}");
        }
    }

    private void SendConfig()
    {
        if (!_coreWebView2Ready) return;
        var t = App.Settings.Terminal;
        var payload = new
        {
            type = "config",
            scrollback = t.Scrollback,
            cursorBlink = t.CursorBlink,
            theme = t.Theme,
            fontFamily = t.FontFamily,
            fontSize = t.FontSize,
            resizeDebounceMs = t.ResizeDebounceMs
        };
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        WebView.CoreWebView2.PostWebMessageAsString(json);
    }

    private void StartTerminal() {
        if (_terminal != null) return;
        var t = App.Settings.Terminal;
        _terminal = new Terminal();
        _terminal.OutputReady += OnTerminalOutputReady;

        var idx = PanelIndex >= 0 ? PanelIndex.ToString() : "default";
        var script = (t.StartupCommand ?? "").Replace("{HOST_IDX}", idx);

        if (!string.IsNullOrWhiteSpace(InitialCwd)) {
            var escapedCwd = InitialCwd.Replace("'", "''");
            script = $"Set-Location -Path '{escapedCwd}'; " + script;
        }

        var args = new System.Collections.Generic.List<string>(t.ShellArgs ?? System.Array.Empty<string>());
        if (!string.IsNullOrWhiteSpace(script)) {
            var bytes = System.Text.Encoding.Unicode.GetBytes(script);
            var encoded = System.Convert.ToBase64String(bytes);
            args.Add("-EncodedCommand");
            args.Add(encoded);
        }

        var fullCommand = $"{t.ShellPath} {string.Join(" ", args)}";

        _terminalThread = new Thread(() => {
            try { _terminal.Start(fullCommand, t.Cols, t.Rows); }
            catch (Exception ex) {
                Dispatcher.InvokeAsync(() => SendError($"Terminal failed: {ex.Message}"));
            }
        }) { IsBackground = true };
        _terminalThread.Start();
    }

    private void OnTerminalOutputReady(object? sender, EventArgs e)
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ReadOutputLoop(_cts.Token));

        if (!string.IsNullOrWhiteSpace(RunOnStart))
        {
            var runCmd = RunOnStart;
            RunOnStart = null;
            _ = Task.Run(async () =>
            {
                await Task.Delay(InitialCdDelayMs);
                try { _terminal?.WriteToPseudoConsole(runCmd + "\r"); } catch { }
            });
        }
    }

    private async Task ReadOutputLoop(CancellationToken ct)
    {
        var buffer = new byte[OutputBufferSize];
        var decoder = Encoding.UTF8.GetDecoder();
        var charBuf = new char[Encoding.UTF8.GetMaxCharCount(OutputBufferSize)];
        try
        {
            while (!ct.IsCancellationRequested && _terminal != null)
            {
                int n = await _terminal.ConsoleOutStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                if (n == 0) break;
                int charCount = decoder.GetChars(buffer, 0, n, charBuf, 0);
                if (charCount > 0)
                {
                    var chunk = new string(charBuf, 0, charCount);
                    await Dispatcher.InvokeAsync(() => SendData(chunk));
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private void SendData(string payload)
    {
        if (!_coreWebView2Ready) return;
        try
        {
            var json = JsonSerializer.Serialize(new { type = "data", payload }, JsonOpts);
            WebView.CoreWebView2.PostWebMessageAsString(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebTerminalPanel] PostWebMessageAsString failed: {ex.Message}");
        }
    }

    private void SendError(string message)
    {
        if (!_coreWebView2Ready) return;
        try
        {
            var json = JsonSerializer.Serialize(new { type = "error", message }, JsonOpts);
            WebView.CoreWebView2.PostWebMessageAsString(json);
        }
        catch { }
    }

    public void SendInput(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { _terminal?.WriteToPseudoConsole(text); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[WebTerminalPanel.SendInput] {ex.Message}"); }
    }

    public void Shutdown()
    {
        try
        {
            _cts?.Cancel();
            try { _terminal?.WriteToPseudoConsole("exit\r"); } catch { }
            _terminalThread?.Join(ShutdownJoinTimeoutMs);
        }
        catch { }
    }

    internal static void DiagLog(string msg) {
        try {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "debug.log"), line + Environment.NewLine);
        } catch { }
    }
}
