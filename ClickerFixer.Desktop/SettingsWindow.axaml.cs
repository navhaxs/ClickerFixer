using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ClickerFixer.Desktop;

public partial class SettingsWindow : Window
{
    private static SettingsWindow? _instance;

    private readonly ObservableCollection<TargetPriorityItem> _items;

    public SettingsWindow()
    {
        InitializeComponent();

        var names = Global.Config.TargetPriority is { Count: > 0 }
            ? Global.Config.TargetPriority
            : new() { "ProPresenter", "VisionScreens", "PowerPoint", "Native" };

        _items = new ObservableCollection<TargetPriorityItem>(names.Select(n => new TargetPriorityItem(n)));
        TargetsListBox.ItemsSource = _items;
        UpdateNativeWarning();
    }

    /// <summary>Opens the single shared settings window, or brings it to front if already open.</summary>
    public static void ShowOrActivate()
    {
        if (_instance == null)
        {
            _instance = new SettingsWindow();
            _instance.Closed += (_, _) => _instance = null;
            _instance.Show();
        }

        _instance.Activate();
        _instance.WindowState = WindowState.Normal;
    }

    private void MoveUpButtonClicked(object? sender, RoutedEventArgs e)
    {
        var index = TargetsListBox.SelectedIndex;
        if (index <= 0)
            return;

        _items.Move(index, index - 1);
        TargetsListBox.SelectedIndex = index - 1;
        UpdateNativeWarning();
    }

    private void MoveDownButtonClicked(object? sender, RoutedEventArgs e)
    {
        var index = TargetsListBox.SelectedIndex;
        if (index < 0 || index >= _items.Count - 1)
            return;

        _items.Move(index, index + 1);
        TargetsListBox.SelectedIndex = index + 1;
        UpdateNativeWarning();
    }

    private void SaveButtonClicked(object? sender, RoutedEventArgs e)
    {
        Global.Config.TargetPriority = _items.Select(i => i.Name).ToList();
        Global.Save();
    }

    private void UpdateNativeWarning()
    {
        var nativeIndex = _items.Select(i => i.Name).ToList().IndexOf("Native");
        NativeOrderWarning.IsVisible = nativeIndex >= 0 && nativeIndex != _items.Count - 1;
    }

    private class TargetPriorityItem
    {
        public string Name { get; }
        private readonly string _displayName;

        public TargetPriorityItem(string name)
        {
            Name = name;
            _displayName = name == "Native" ? "send key events" : name;
        }

        public override string ToString() => _displayName;
    }
}
