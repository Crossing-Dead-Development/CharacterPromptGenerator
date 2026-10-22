using CharacterPromptGenerator.Mobile.ViewModels;

namespace CharacterPromptGenerator.Mobile.Views;

public partial class DrawSettingsPage : ContentPage
{
    public const string Route = "drawsettings";

    readonly DrawSettingsViewModel _vm;

    public DrawSettingsPage(DrawSettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
    }
}
