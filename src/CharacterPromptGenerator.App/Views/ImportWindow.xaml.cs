using System.Windows;
using CharacterPromptGenerator.App.ViewModels;

namespace CharacterPromptGenerator.App.Views;

public partial class ImportWindow : Window
{
    public ImportWindow(ImportViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += ok => DialogResult = ok;
        Loaded += async (_, _) => await vm.LoadAsync();
    }
}
