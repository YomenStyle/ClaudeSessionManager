using System.Windows;
using System.Windows.Controls;
namespace ClaudeSessionManager.Wpf.Views;
public partial class MainWindow : Window {
    private bool _initialized;
    public MainWindow() {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    private void OnLoaded(object sender, RoutedEventArgs e) {
        var mode = App.Settings.Terminal.DefaultMode;
        ModeTabs.SelectedIndex = System.Math.Clamp(mode - 1, 0, 3);
        SplitArea.SetMode(mode);
        Sidebar.Width = App.Settings.Sidebar.SidebarWidth;
        _initialized = true;
    }
    private void ModeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        if (!_initialized) return;
        if (e.Source is not TabControl tc) return;
        var mode = tc.SelectedIndex + 1;
        if (mode is >= 1 and <= 4) SplitArea.SetMode(mode);
    }
}
