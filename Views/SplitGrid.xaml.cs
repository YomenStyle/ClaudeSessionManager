using System.Windows;
using System.Windows.Controls;
using ClaudeSessionManager.Wpf.Models;
namespace ClaudeSessionManager.Wpf.Views;
public partial class SplitGrid : UserControl {
    private const int SplitterSize = 5;
    internal const int MaxSplit = 4;
    private readonly ConsoleTabHost?[] _persistentHosts = new ConsoleTabHost?[MaxSplit];
    public int CurrentMode { get; private set; }
    public SplitGrid() { InitializeComponent(); }

    public void SetMode(int mode) {
        if (mode < 1 || mode > 4) throw new System.ArgumentOutOfRangeException(nameof(mode));
        // detach persistent hosts from their current parents
        for (int i = 0; i < _persistentHosts.Length; i++) {
            var h = _persistentHosts[i];
            if (h == null) continue;
            if (h.Parent is Panel p) p.Children.Remove(h);
            else if (h.Parent is Decorator d) d.Child = null;
            else if (h.Parent is ContentControl cc) cc.Content = null;
        }
        Root.Children.Clear();
        Root.RowDefinitions.Clear();
        Root.ColumnDefinitions.Clear();

        var t = App.Settings.Terminal;

        switch (mode) {
            case 1: BuildMode1(t); break;
            case 2: BuildMode2(t); break;
            case 3: BuildMode3(t); break;
            case 4: BuildMode4(t); break;
        }
        CurrentMode = mode;
    }

    private ConsoleTabHost GetOrCreateHost(int idx) {
        _persistentHosts[idx] ??= new ConsoleTabHost { HostIndex = idx };
        return _persistentHosts[idx]!;
    }

    public ConsoleTabHost? GetPersistentHostOrNull(int idx) {
        if (idx < 0 || idx >= _persistentHosts.Length) return null;
        return _persistentHosts[idx];
    }

    public void OpenNewSplit(string folder, string? runOnStart) {
        var newMode = CurrentMode < MaxSplit ? CurrentMode + 1 : CurrentMode;

        if (newMode > CurrentMode) {
            var targetIdx = newMode - 1;
            if (_persistentHosts[targetIdx] == null) {
                _persistentHosts[targetIdx] = new ConsoleTabHost { HostIndex = targetIdx };
            }
            _persistentHosts[targetIdx]!.PendingFirstTabCwd = folder;
            _persistentHosts[targetIdx]!.PendingFirstTabRunOnStart = runOnStart;
            SetMode(newMode);
        } else {
            // 모드 4 이상 — 첫 호스트에 새 탭 추가
            var host = _persistentHosts[0];
            if (host != null) host.AddConsoleTabWithCwd(folder, runOnStart);
        }
    }

    private void BuildMode1(TerminalSettings t) {
        Root.ColumnDefinitions.Add(new ColumnDefinition());
        Root.RowDefinitions.Add(new RowDefinition());
        var h = GetOrCreateHost(0);
        Root.Children.Add(h);
    }

    private void BuildMode2(TerminalSettings t) {
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SplitterSize) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.RowDefinitions.Add(new RowDefinition());

        var h1 = GetOrCreateHost(0); Grid.SetColumn(h1, 0); Root.Children.Add(h1);
        var sp = new GridSplitter {
            ResizeBehavior     = GridResizeBehavior.PreviousAndNext,
            ResizeDirection    = GridResizeDirection.Columns,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment  = VerticalAlignment.Stretch,
            Width              = SplitterSize,
            Background         = System.Windows.Media.Brushes.Gray
        };
        Grid.SetColumn(sp, 1); Root.Children.Add(sp);
        var h2 = GetOrCreateHost(1); Grid.SetColumn(h2, 2); Root.Children.Add(h2);
    }

    private void BuildMode3(TerminalSettings t) {
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SplitterSize) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.RowDefinitions.Add(new RowDefinition());

        var hLeft = GetOrCreateHost(0); Grid.SetColumn(hLeft, 0); Root.Children.Add(hLeft);

        var outerSp = new GridSplitter {
            ResizeBehavior     = GridResizeBehavior.PreviousAndNext,
            ResizeDirection    = GridResizeDirection.Columns,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment  = VerticalAlignment.Stretch,
            Width              = SplitterSize,
            Background         = System.Windows.Media.Brushes.Gray
        };
        Grid.SetColumn(outerSp, 1); Root.Children.Add(outerSp);

        var inner = new Grid();
        inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SplitterSize) });
        inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var hRT = GetOrCreateHost(1); Grid.SetRow(hRT, 0); inner.Children.Add(hRT);
        var innerSp = new GridSplitter {
            ResizeBehavior     = GridResizeBehavior.PreviousAndNext,
            ResizeDirection    = GridResizeDirection.Rows,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment  = VerticalAlignment.Stretch,
            Height             = SplitterSize,
            Background         = System.Windows.Media.Brushes.Gray
        };
        Grid.SetRow(innerSp, 1); inner.Children.Add(innerSp);
        var hRB = GetOrCreateHost(2); Grid.SetRow(hRB, 2); inner.Children.Add(hRB);
        Grid.SetColumn(inner, 2); Root.Children.Add(inner);
    }

    private void BuildMode4(TerminalSettings t) {
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SplitterSize) });
        Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SplitterSize) });
        Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var h00 = GetOrCreateHost(0); Grid.SetRow(h00, 0); Grid.SetColumn(h00, 0); Root.Children.Add(h00);
        var h02 = GetOrCreateHost(1); Grid.SetRow(h02, 0); Grid.SetColumn(h02, 2); Root.Children.Add(h02);
        var h20 = GetOrCreateHost(2); Grid.SetRow(h20, 2); Grid.SetColumn(h20, 0); Root.Children.Add(h20);
        var h22 = GetOrCreateHost(3); Grid.SetRow(h22, 2); Grid.SetColumn(h22, 2); Root.Children.Add(h22);

        var hSp = new GridSplitter {
            ResizeBehavior     = GridResizeBehavior.PreviousAndNext,
            ResizeDirection    = GridResizeDirection.Rows,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment  = VerticalAlignment.Stretch,
            Height             = SplitterSize,
            Background         = System.Windows.Media.Brushes.Gray
        };
        Grid.SetRow(hSp, 1); Grid.SetColumn(hSp, 0); Grid.SetColumnSpan(hSp, 3); Root.Children.Add(hSp);

        var vSp = new GridSplitter {
            ResizeBehavior     = GridResizeBehavior.PreviousAndNext,
            ResizeDirection    = GridResizeDirection.Columns,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment  = VerticalAlignment.Stretch,
            Width              = SplitterSize,
            Background         = System.Windows.Media.Brushes.Gray
        };
        Grid.SetColumn(vSp, 1); Grid.SetRow(vSp, 0); Grid.SetRowSpan(vSp, 3); Root.Children.Add(vSp);
    }
}
