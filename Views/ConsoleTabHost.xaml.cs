using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClaudeSessionManager.Wpf.Views;

public partial class ConsoleTabHost : UserControl {
    private int _tabCounter;
    private bool _isAddingTab;
    private TabItem? _plusTab;

    public int HostIndex { get; set; }
    public WebTerminalPanel? FirstTab { get; private set; }
    public string? PendingFirstTabCwd { get; set; }
    public string? PendingFirstTabRunOnStart { get; set; }

    public ConsoleTabHost() {
        InitializeComponent();
        this.PreviewMouseDown += ConsoleTabHost_PreviewMouseDown;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) {
        if (_plusTab != null) return;
        _plusTab = new TabItem {
            Header = CreatePlusHeader(),
            IsHitTestVisible = true
        };
        Tabs.Items.Add(_plusTab);
        AddConsoleTab();
    }

    private void AddConsoleTab() {
        _isAddingTab = true;
        try {
            _tabCounter++;
            var panel = new WebTerminalPanel();
            if (_tabCounter == 1) {
                string? initial = PendingFirstTabCwd;
                string? runOnStart = PendingFirstTabRunOnStart;

                if (initial == null && !string.IsNullOrWhiteSpace(App.OverrideCwd)) {
                    initial = App.OverrideCwd;
                    runOnStart ??= App.Settings.Terminal.OnOpenCommand;
                }

                if (initial == null) {
                    var lastCwds = App.Settings.Terminal.LastCwds;
                    if (HostIndex >= 0 && HostIndex < lastCwds.Length) {
                        var c = lastCwds[HostIndex];
                        if (!string.IsNullOrWhiteSpace(c)) initial = c;
                    }
                }

                if (!string.IsNullOrWhiteSpace(initial)) panel.InitialCwd = initial;
                if (!string.IsNullOrWhiteSpace(runOnStart)) panel.RunOnStart = runOnStart;
                panel.PanelIndex = HostIndex;
                FirstTab = panel;
                PendingFirstTabCwd = null;
                PendingFirstTabRunOnStart = null;
            } else {
                // 후속 탭: 같은 호스트의 가장 최근 콘솔 탭 CWD 상속
                string? inheritedCwd = null;
                for (int i = Tabs.Items.Count - 1; i >= 0; i--) {
                    if (Tabs.Items[i] is TabItem prevTab && prevTab != _plusTab && prevTab.Content is WebTerminalPanel prevPanel) {
                        inheritedCwd = prevPanel.LastCwd ?? prevPanel.InitialCwd;
                        if (!string.IsNullOrWhiteSpace(inheritedCwd)) break;
                    }
                }
                if (string.IsNullOrWhiteSpace(inheritedCwd)) {
                    var lastCwds = App.Settings.Terminal.LastCwds;
                    if (HostIndex >= 0 && HostIndex < lastCwds.Length) {
                        var c = lastCwds[HostIndex];
                        if (!string.IsNullOrWhiteSpace(c)) inheritedCwd = c;
                    }
                }
                if (!string.IsNullOrWhiteSpace(inheritedCwd)) panel.InitialCwd = inheritedCwd;
                panel.PanelIndex = HostIndex;
            }
            var tab = new TabItem {
                Header = CreateTabHeader($"PS {_tabCounter}", out Button closeBtn),
                Content = panel
            };
            closeBtn.Click += (s, e) => CloseTab(tab);
            int insertIdx = _plusTab != null ? Tabs.Items.IndexOf(_plusTab) : Tabs.Items.Count;
            Tabs.Items.Insert(insertIdx, tab);
            Tabs.SelectedItem = tab;
            // 새 탭 생성 후 ActivePanel로 즉시 전환 (_isAddingTab 가드로 Tabs_SelectionChanged의 TryActivateCurrentPanel이 차단되므로 명시 호출)
            App.SetActivePanel(panel);
            panel.FocusWebView();
        } finally { _isAddingTab = false; }
    }

    public void AddConsoleTabWithCwd(string cwd, string? runOnStart) {
        _isAddingTab = true;
        try {
            _tabCounter++;
            var panel = new WebTerminalPanel { InitialCwd = cwd };
            if (!string.IsNullOrWhiteSpace(runOnStart)) panel.RunOnStart = runOnStart;
            var tab = new TabItem {
                Header = CreateTabHeader($"PS {_tabCounter}", out Button closeBtn),
                Content = panel
            };
            closeBtn.Click += (s, e) => CloseTab(tab);
            int insertIdx = _plusTab != null ? Tabs.Items.IndexOf(_plusTab) : Tabs.Items.Count;
            Tabs.Items.Insert(insertIdx, tab);
            Tabs.SelectedItem = tab;
            // 새 탭 생성 후 ActivePanel로 즉시 전환 (_isAddingTab 가드로 Tabs_SelectionChanged의 TryActivateCurrentPanel이 차단되므로 명시 호출)
            App.SetActivePanel(panel);
            panel.FocusWebView();
        } finally { _isAddingTab = false; }
    }

    private void CloseTab(TabItem tab) {
        if (tab.Content is WebTerminalPanel cp) {
            try { cp.Shutdown(); } catch { }
        }

        int idx = Tabs.Items.IndexOf(tab);
        int remainingConsoleAfter = (Tabs.Items.Count - 1) - (_plusTab != null ? 1 : 0);

        // Remove 전: 닫는 탭이 selected이고 남은 콘솔 탭이 있으면 인접 콘솔 탭으로 미리 이동.
        // 그렇지 않으면 Remove가 자동으로 _plusTab selected 만들고 SelectionChanged가 AddConsoleTab 호출 (마지막 콘솔 탭 케이스).
        if (remainingConsoleAfter > 0 && Tabs.SelectedItem == tab) {
            TabItem? target = null;
            if (idx + 1 < Tabs.Items.Count && Tabs.Items[idx + 1] is TabItem next && next != _plusTab) {
                target = next;
            } else if (idx - 1 >= 0 && Tabs.Items[idx - 1] is TabItem prev && prev != _plusTab) {
                target = prev;
            }
            if (target != null) Tabs.SelectedItem = target;
        }

        Tabs.Items.Remove(tab);
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (_isAddingTab) return;
        if (Tabs.SelectedItem == _plusTab) {
            AddConsoleTab();
            return;
        }
        TryActivateCurrentPanel();
    }

    private void ConsoleTabHost_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        TryActivateCurrentPanel();
    }

    private void TryActivateCurrentPanel()
    {
        if (Tabs.SelectedItem is System.Windows.Controls.TabItem ti && ti.Content is WebTerminalPanel panel)
        {
            App.SetActivePanel(panel);
            panel.FocusWebView();
        }
    }

    private static object CreatePlusHeader() {
        return new TextBlock {
            Text = "+",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Padding = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static object CreateTabHeader(string title, out Button closeBtn) {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var label = new TextBlock {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        label.MouseLeftButtonDown += (s, e) => {
            if (e.ClickCount == 2) { BeginRename(sp, label); e.Handled = true; }
        };
        sp.Children.Add(label);
        closeBtn = new Button {
            Content = "×",
            Width = 16, Height = 16,
            FontSize = 12,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand
        };
        sp.Children.Add(closeBtn);
        return sp;
    }

    private static void BeginRename(StackPanel sp, TextBlock label) {
        var tb = new TextBox {
            Text = label.Text, MinWidth = 60,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        int idx = sp.Children.IndexOf(label);
        sp.Children.RemoveAt(idx);
        sp.Children.Insert(idx, tb);
        tb.Focus();
        tb.SelectAll();
        void Commit() {
            label.Text = string.IsNullOrWhiteSpace(tb.Text) ? label.Text : tb.Text;
            int j = sp.Children.IndexOf(tb);
            if (j < 0) return;
            sp.Children.RemoveAt(j);
            sp.Children.Insert(j, label);
        }
        tb.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Escape) Commit(); };
        tb.LostFocus += (s, e) => Commit();
    }
}
