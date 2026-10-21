using System.Windows;

namespace CharacterPromptGenerator.App.Views;

public partial class InputDialog : Window
{
    public InputDialog(string title, string label, string initial)
    {
        InitializeComponent();
        Title = title;
        LabelText.Text = label;
        ValueBox.Text = initial;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    public string Value => ValueBox.Text;

    void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
