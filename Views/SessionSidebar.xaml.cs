using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeSessionManager.Wpf.Models;
using ClaudeSessionManager.Wpf.Services;
using ClaudeSessionManager.Wpf.Views.Dialogs;

namespace ClaudeSessionManager.Wpf.Views;

public partial class SessionSidebar : UserControl
{
    private record PanelOption(string Display, WebTerminalPanel Panel);

    private readonly ClaudeSessionScanner _scanner = new();
    private readonly DispatcherTimer _timer = new();
    private CancellationTokenSource? _scanCts;
    private SidebarSettings _settings = new();
    private bool _suppressSelectionEvent;
    private const string FallbackSessionTitle = "(제목 없음)";
    private const string EditTitleErrorCaption = "제목 편집 실패";

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
                .Select(e => new SessionListItem(e.AiTitle ?? FallbackSessionTitle, e.FirstMessage, e))
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

    private async Task EditTitleAsync(SessionListItem? item)
    {
        if (item == null) return;
        var currentTitle = item.Source.AiTitle ?? string.Empty;
        var dlg = new EditTitleDialog(currentTitle)
        {
            Owner = Window.GetWindow(this)
        };
        if (dlg.ShowDialog() != true) return;
        var newTitle = dlg.ResultTitle;
        if (string.IsNullOrWhiteSpace(newTitle)) return;
        if (newTitle == currentTitle) return;

        try
        {
            await SessionTitleWriter.AppendTitleAsync(
                item.Source.FilePath,
                item.Source.SessionId,
                newTitle);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditTitle.Async FATAL] {ex}");
            MessageBox.Show(
                Window.GetWindow(this),
                ex.Message,
                EditTitleErrorCaption,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void OnEditTitleMenuClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var item = (sender as MenuItem)?.DataContext as SessionListItem;
            System.Diagnostics.Debug.WriteLine($"[EditTitle.MenuClick] sender={sender?.GetType().Name} item={item?.AiTitle ?? "<null>"}");
            await EditTitleAsync(item);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditTitle.MenuClick FATAL] {ex}");
            MessageBox.Show(Window.GetWindow(this), $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", "메뉴 클릭 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnSessionListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F2)
        {
            e.Handled = true;
            try
            {
                await EditTitleAsync(SessionList.SelectedItem as SessionListItem);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EditTitle.F2 FATAL] {ex}");
                MessageBox.Show(Window.GetWindow(this), $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", "F2 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnSessionSelected(object sender, SelectionChangedEventArgs e)
    {
        // 선택만 처리. resume은 PreviewMouseLeftButtonDown에서.
    }

    private void OnSessionListPreviewRightDown(object sender, MouseButtonEventArgs e)
    {
        // 우클릭 전용 로직 예약 지점.
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject? obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T t) return t;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }

    private async Task ResumeSessionAsync(SessionListItem item)
    {
        var panel = App.ActivePanel;
        if (panel == null) return;
        panel.SendInput("/exit\r");
        await Task.Delay(1500);
        panel.SendInput($"claude --resume {item.Source.SessionId}\r");
    }

    private async void OnSessionListPreviewMouseLeftDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var dep = e.OriginalSource as DependencyObject;
            var lbItem = FindAncestorOrSelf<ListBoxItem>(dep);
            if (lbItem == null) return;

            var clicked = lbItem.DataContext as SessionListItem;
            if (clicked == null) return;

            if (ReferenceEquals(SessionList.SelectedItem, clicked))
            {
                e.Handled = true;
                await ResumeSessionAsync(clicked);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Click.LeftDown FATAL] {ex}");
        }
    }

    private static string FormatCwdShort(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "(no cwd)";
        var trimmed = path.TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(trimmed)) return path;
        var parts = trimmed.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && parts[0].EndsWith(':'))
        {
            parts = parts.Skip(1).ToArray();
        }
        if (parts.Length == 0) return trimmed;
        if (parts.Length <= 2) return string.Join("\\", parts);
        return string.Join("\\", parts.Skip(parts.Length - 2));
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

    private sealed record SessionListItem(string AiTitle, string FirstMessage, SessionEntry Source);

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
                            var cwdShort = FormatCwdShort(effectiveCwd);
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
