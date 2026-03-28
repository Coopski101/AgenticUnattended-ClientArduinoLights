using Avalonia.Controls;

namespace ArduinoBridge;

public partial class MainWindow : Window
{
    public bool ForceClose { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");
        if (File.Exists(iconPath))
            Icon = new WindowIcon(iconPath);
    }

    public void AppendLog(string message)
    {
        LogBox.Text += message + Environment.NewLine;
        if ((LogBox.Text?.Length ?? 0) > 50000)
            LogBox.Text = LogBox.Text![^25000..];
        LogBox.CaretIndex = int.MaxValue;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!ForceClose)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
