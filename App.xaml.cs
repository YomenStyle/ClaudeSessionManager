using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ClaudeSessionManager.Wpf.Models;
using Microsoft.Web.WebView2.Core;
namespace ClaudeSessionManager.Wpf;

public partial class App : Application
{
    private const string MutexName = @"Global\ClaudeSessionManager_SingleInstance_v1";
    private const string PipeName = "ClaudeSessionManager_IPC_v1";
    private Mutex? _instanceMutex;

    public static AppSettings Settings { get; private set; } = null!;
    public static string? OverrideCwd { get; private set; }

    public static Views.WebTerminalPanel? ActivePanel { get; private set; }
    public static event EventHandler? ActivePanelChanged;
    public static event EventHandler? ActivePanelCwdChanged;

    public static CoreWebView2Environment? SharedWebView2Env { get; private set; }

    public static void SetActivePanel(Views.WebTerminalPanel panel)
    {
        if (panel == null) return;
        if (!ReferenceEquals(ActivePanel, panel))
        {
            ActivePanel = panel;
            ActivePanelChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void NotifyCwdChanged(Views.WebTerminalPanel panel)
    {
        if (panel == null) return;
        if (ReferenceEquals(ActivePanel, panel))
            ActivePanelCwdChanged?.Invoke(null, EventArgs.Empty);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0 && System.IO.Directory.Exists(e.Args[0])) {
            OverrideCwd = e.Args[0];
        }

        _instanceMutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew) {
            // 기존 인스턴스에 폴더 송신 후 종료
            if (!string.IsNullOrWhiteSpace(OverrideCwd)) {
                try {
                    using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                    client.Connect(2000);
                    using var writer = new StreamWriter(client) { AutoFlush = true };
                    writer.WriteLine(OverrideCwd);
                } catch { }
            }
            Shutdown();
            return;
        }

        _ = Task.Run(PipeListenerLoop);

        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            throw new InvalidOperationException($"appsettings.json not found: {path}");
        var json = File.ReadAllText(path);
        Settings = JsonSerializer.Deserialize<AppSettings>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Failed to parse appsettings.json");
        _ = Task.Run(async () => {
            try {
                var env = await CoreWebView2Environment.CreateAsync();
                SharedWebView2Env = env;
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[WebView2 pre-warm] {ex.Message}");
            }
        });
        base.OnStartup(e);
    }

    private async Task PipeListenerLoop() {
        while (true) {
            try {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                await server.WaitForConnectionAsync();
                using var reader = new StreamReader(server);
                var folder = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)) {
                    Dispatcher.Invoke(() => {
                        if (MainWindow is Views.MainWindow mw) {
                            mw.SplitArea.OpenNewSplit(folder, Settings.Terminal.OnOpenCommand);
                        }
                    });
                }
            } catch { await Task.Delay(500); }
        }
    }

    protected override void OnExit(ExitEventArgs e) {
        Views.WebTerminalPanel.DiagLog("App.OnExit ENTER");
        try { SaveState(); Views.WebTerminalPanel.DiagLog("App.SaveState OK"); }
        catch (Exception ex) { Views.WebTerminalPanel.DiagLog($"App.SaveState ERROR: {ex.Message}"); }
        try { _instanceMutex?.ReleaseMutex(); _instanceMutex?.Dispose(); } catch { }
        base.OnExit(e);
    }

    private void SaveState() {
        var lastCwds = new string[4];
        var tempDir = Path.GetTempPath();
        for (int i = 0; i < 4; i++) {
            var tempFile = Path.Combine(tempDir, $"csm_cwd_{i}.txt");
            if (File.Exists(tempFile)) {
                try { lastCwds[i] = File.ReadAllText(tempFile, Encoding.UTF8); }
                catch { lastCwds[i] = ""; }
            } else {
                lastCwds[i] = "";
            }
        }
        Views.WebTerminalPanel.DiagLog($"SaveState collected: [{string.Join(", ", lastCwds.Select(c => $"\"{c}\""))}]");
        Settings.Terminal.LastCwds = lastCwds;
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
