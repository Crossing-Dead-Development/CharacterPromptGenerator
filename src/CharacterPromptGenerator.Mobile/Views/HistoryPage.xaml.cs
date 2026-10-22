using CharacterPromptGenerator.Mobile.ViewModels;

namespace CharacterPromptGenerator.Mobile.Views;

public partial class HistoryPage : ContentPage
{
    readonly HistoryViewModel _vm;

    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
    }

    void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is HistoryRow row) _vm.TapCommand.Execute(row);
    }
}
