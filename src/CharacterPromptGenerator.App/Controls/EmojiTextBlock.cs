using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.Core.Emoji;
using Image = System.Windows.Controls.Image;

namespace CharacterPromptGenerator.App.Controls;

/// <summary>
/// 顯示含 Emoji 的文字：Emoji 以 Fluent Emoji 3D 圖片（WPF Image）呈現，其餘維持一般文字。
/// 綁定的 <see cref="Value"/> 就是詞庫中的原始 Unicode 字串，本控制項不會修改它。
/// 找不到圖片的 Emoji 顯示「?」替代圖示，滑鼠移上去會說明缺少哪個碼點，以及可以補圖的檔名。
/// </summary>
public class EmojiTextBlock : TextBlock
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(EmojiTextBlock), new FrameworkPropertyMetadata(null, OnLayoutInputChanged));

    /// <summary>
    /// true：這是 Emoji 欄位，每個字元都先嘗試找圖（例如沒有 FE0F 的 ☂ 也會顯示成圖片）。
    /// false：一般文字，只有「一定是 Emoji」的字元才換成圖片，避免 © 之類的符號被換掉。
    /// </summary>
    public static readonly DependencyProperty IsEmojiFieldProperty = DependencyProperty.Register(
        nameof(IsEmojiField), typeof(bool), typeof(EmojiTextBlock), new FrameworkPropertyMetadata(false, OnLayoutInputChanged));

    /// <summary>Emoji 圖片相對於字體大小的倍率。</summary>
    public static readonly DependencyProperty EmojiScaleProperty = DependencyProperty.Register(
        nameof(EmojiScale), typeof(double), typeof(EmojiTextBlock), new FrameworkPropertyMetadata(1.25, OnLayoutInputChanged));

    public string? Value
    {
        get => (string?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsEmojiField
    {
        get => (bool)GetValue(IsEmojiFieldProperty);
        set => SetValue(IsEmojiFieldProperty, value);
    }

    public double EmojiScale
    {
        get => (double)GetValue(EmojiScaleProperty);
        set => SetValue(EmojiScaleProperty, value);
    }

    static void OnLayoutInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EmojiTextBlock)d).Rebuild();

    void Rebuild()
    {
        Inlines.Clear();
        var value = Value;
        AutomationProperties.SetName(this, value ?? "");
        if (string.IsNullOrEmpty(value)) return;

        if (IsEmojiField)
        {
            foreach (var cluster in EmojiText.Clusters(value))
            {
                if (string.IsNullOrWhiteSpace(cluster))
                {
                    Inlines.Add(new Run(cluster));
                    continue;
                }
                if (EmojiImages.Get(cluster) is { } image) AddImage(image);
                else if (EmojiText.IsDefinitelyEmoji(cluster)) AddEmoji(cluster);
                else Inlines.Add(new Run(cluster));
            }
            return;
        }

        foreach (var segment in EmojiText.Split(value))
        {
            if (segment.IsEmoji) AddEmoji(segment.Text);
            else Inlines.Add(new Run(segment.Text));
        }
    }

    /// <summary>整個序列有圖就用；ZWJ 組合沒有圖時並排顯示各元素；仍然不行才顯示替代圖示。</summary>
    void AddEmoji(string cluster)
    {
        if (EmojiImages.Get(cluster) is { } image)
        {
            AddImage(image);
            return;
        }
        var parts = EmojiText.ZwjComponents(cluster);
        if (parts.Count > 1)
        {
            var images = parts.Select(EmojiImages.Get).ToList();
            if (images.All(i => i is not null))
            {
                foreach (var i in images) AddImage(i!);
                return;
            }
        }
        AddPlaceholder(cluster);
    }

    /// <summary>圖片尺寸跟著 FontSize 走（展示模式調整字體時不必重建）。</summary>
    Binding SizeBinding() => new(nameof(FontSize)) { Source = this, Converter = EmojiSizeConverter.Instance, ConverterParameter = EmojiScale };

    double CurrentSize => EmojiSizeConverter.Size(FontSize, EmojiScale);

    void AddImage(ImageSource source)
    {
        var image = new Image
        {
            Source = source,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(1, 0, 1, 0),
        };
        image.SetBinding(WidthProperty, SizeBinding());
        image.SetBinding(HeightProperty, SizeBinding());
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        Inlines.Add(new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Center });
    }

    void AddPlaceholder(string cluster)
    {
        var size = CurrentSize;
        var key = EmojiText.Key(cluster);
        var muted = TryFindResource("TextMuted") as Brush ?? Brushes.Gray;
        var box = new Border
        {
            Margin = new Thickness(1, 0, 1, 0),
            CornerRadius = new CornerRadius(size * 0.22),
            BorderThickness = new Thickness(Math.Max(1, size / 20)),
            BorderBrush = muted,
            Background = new SolidColorBrush(Color.FromArgb(0x22, 0x80, 0x80, 0x80)),
            Child = new Viewbox // 「?」隨外框大小縮放
            {
                Margin = new Thickness(size * 0.18),
                Child = new TextBlock { Text = "?", FontWeight = FontWeights.Bold, Foreground = muted },
            },
            ToolTip = $"找不到這個 Emoji 的圖片：{cluster}\n碼點：{EmojiText.Describe(cluster)}\n" +
                      $"可以把 PNG 圖片命名為 {EmojiLibrary.OverrideFileName(key)}，放到資料夾的 EmojiOverrides\\ 中。",
        };
        box.SetBinding(WidthProperty, SizeBinding());
        box.SetBinding(HeightProperty, SizeBinding());
        Inlines.Add(new InlineUIContainer(box) { BaselineAlignment = BaselineAlignment.Center });
    }
}

sealed class EmojiSizeConverter : IValueConverter
{
    public static readonly EmojiSizeConverter Instance = new();

    public static double Size(double fontSize, double scale) => Math.Max(8, fontSize * scale);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Size(value is double d ? d : 12, parameter is double s ? s : 1.25);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
