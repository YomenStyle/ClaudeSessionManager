using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClaudeSessionManager.Wpf.Views.Dialogs;

public partial class EditTitleDialog : Window
{
    public string ResultTitle { get; private set; } = string.Empty;

    public EditTitleDialog(string currentTitle)
    {
        InitializeComponent();
        TitleBox.Text = currentTitle ?? string.Empty;
        Loaded += (_, _) => { TitleBox.Focus(); TitleBox.SelectAll(); };
        UpdateOkEnabled();
    }

    private void OnTitleChanged(object sender, TextChangedEventArgs e) => UpdateOkEnabled();

    private void UpdateOkEnabled() => OkButton.IsEnabled = !string.IsNullOrWhiteSpace(TitleBox.Text);

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text)) return;
        ResultTitle = TitleBox.Text.Trim();
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && OkButton.IsEnabled)
        {
            e.Handled = true;
            OnOkClick(sender, new RoutedEventArgs());
        }
    }
}
