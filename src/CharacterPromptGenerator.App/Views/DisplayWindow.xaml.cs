using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CharacterPromptGenerator.App.ViewModels;

namespace CharacterPromptGenerator.App.Views;

/// <summary>直播展示視窗：與主畫面共用同一個 DrawViewModel，兩邊即時同步。</summary>
public partial class DisplayWindow : Window
{
    static DisplayWindow? _instance;

    DisplayWindow(DrawViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Closed += (_, _) => _instance = null;
    }

    public static void ShowFor(DrawViewModel vm)
    {
        if (_instance is not null)
        {
            if (_instance.WindowState == WindowState.Minimized) _instance.WindowState = WindowState.Normal;
            _instance.Activate();
            return;
        }
        _instance = new DisplayWindow(vm) { Owner = Application.Current.MainWindow };
        _instance.Show();
    }

    void ToggleControls_Click(object sender, RoutedEventArgs e) => ToggleControls();

    void ToggleControls()
    {
        if (DataContext is DrawViewModel vm) vm.DisplayShowControls = !vm.DisplayShowControls;
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.None && e.OriginalSource is not TextBox)
        {
            ToggleControls();
            e.Handled = true;
        }
    }
}
