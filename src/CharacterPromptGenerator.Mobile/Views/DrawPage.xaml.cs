using CharacterPromptGenerator.Mobile.ViewModels;

namespace CharacterPromptGenerator.Mobile.Views;

public partial class DrawPage : ContentPage
{
    readonly DrawViewModel _vm;

    public DrawPage(DrawViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Refresh();
    }
}
