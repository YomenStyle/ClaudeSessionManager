using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClaudeSessionManager.Wpf.Models;
using ClaudeSessionManager.Wpf.Services;

namespace ClaudeSessionManager.Wpf.Views;

public partial class SessionSidebar : UserControl
{
    private record PanelOption(string Display, WebTerminalPanel Panel);

    private readonly ClaudeSessionScanner _scanner = new();
    private readonly DispatcherTimer _timer = new();
    private CancellationTokenSource? _scanCts;
    private SidebarSettings _settings = new();
    private bool _suppressSelectionEvent;

    public SessionSidebar()
    {
        InitializeComponent();

        _settings = App.Settings.Sidebar;
        _timer.Interval = TimeSpan.FromMilliseconds(_settings.AutoRefreshIntervalMs);
        _timer.Tick += async (s, e) => { RebuildPanelList(); await RefreshAsync(); };

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.ActivePanelChanged    += OnActiveChanged;
        App.ActivePanelCwdChanged += OnActiveChanged;
        _timer.Start();
        _ = RefreshAsync();
        RebuildPanelList();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.ActivePanelChanged    -= OnActiveChanged;
        App.ActivePanelCwdChanged -= OnActiveChanged;
        _timer.Stop();
        _scanCts?.Cancel();
    }

    private void OnActiveChanged(object? sender, EventArgs e)
        => _ = Dispatcher.InvokeAsync(() => { RebuildPanelList(); return RefreshAsync(); });

    private async Task RefreshAsync()
    {
        try
        {
            var panel = App.ActivePanel;
            var cwd   = panel?.LastCwd ?? panel?.InitialCwd ?? string.Empty;

            if (string.IsNullOrEmpty(cwd))
            {
                SessionList.ItemsSource = null;
                StatusText.Text = string.Empty;
                return;
            }

            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();
            var ct = _scanCts.Token;

            var entries = await _scanner.ScanAsync(cwd, _settings.MaxSessions, ct);
            if (ct.IsCancellationRequested) return;

            var items = entries
                .Select(e => new SessionListItem(FormatRelative(e.LastModified), e.FirstMessage, e))
                .ToList();

            SessionList.ItemsSource = items;
            StatusText.Text = $"{items.Count} sessions";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Sidebar.Refresh] {ex}");
            StatusText.Text = "error";
        }
    }

    private async void OnSessionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SessionList.SelectedItem is not SessionListItem item) return;
        var panel = App.ActivePanel;
        if (panel == null) return;
        panel.SendInput("/exit\r");
        await Task.Delay(1500);
        panel.SendInput($"claude --resume {item.Source.SessionId}\r");
        SessionList.SelectedItem = null;
    }

    private static string FormatRelative(DateTime t)
    {
        var diff = DateTime.Now - t;
        if (diff.TotalMinutes < 1)  return "방금";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}분 전";
        if (t.Date == DateTime.Today)                 return $"오늘 {t:HH:mm}";
        if (t.Date == DateTime.Today.AddDays(-1))     return $"어제 {t:HH:mm}";
        return t.ToString("MM-dd HH:mm");
    }

    private sealed record SessionListItem(string DisplayTime, string FirstMessage, SessionEntry Source);

    private void RebuildPanelList()
    {
        try
        {
            var items = new List<PanelOption>();
            if (Application.Current?.MainWindow is MainWindow mw && mw.SplitArea != null)
            {
                for (int hi = 0; hi < SplitGrid.MaxSplit; hi++)
                {
                    var host = mw.SplitArea.GetPersistentHostOrNull(hi);
                    if (host == null) continue;
                    for (int ti = 0; ti < host.Tabs.Items.Count; ti++)
                    {
                        if (host.Tabs.Items[ti] is TabItem tabItem && tabItem.Content is WebTerminalPanel p)
                        {
                            var effectiveCwd = p.LastCwd ?? p.InitialCwd ?? "";
                            var cwdShort = string.IsNullOrEmpty(effectiveCwd)
                                ? "(no cwd)"
                                : Path.GetFileName(effectiveCwd.TrimEnd('\\', '/'));
                            if (string.IsNullOrEmpty(cwdShort)) cwdShort = effectiveCwd;
                            items.Add(new PanelOption($"분할{hi + 1}/탭{ti + 1} {cwdShort}", p));
                        }
                    }
                }
            }
            _suppressSelectionEvent = true;
            try
            {
                PanelSelector.ItemsSource = items;
                var active = App.ActivePanel;
                int activeIdx = active != null ? items.FindIndex(o => ReferenceEquals(o.Panel, active)) : -1;
                PanelSelector.SelectedIndex = activeIdx;
            }
            finally { _suppressSelectionEvent = false; }
        }
        catch { /* UI 보조 기능 — 실패 무시 */ }
    }

    private void OnPanelSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionEvent) return;
        if (PanelSelector.SelectedItem is PanelOption opt)
        {
            App.SetActivePanel(opt.Panel);
            _ = RefreshAsync();
        }
    }
}
