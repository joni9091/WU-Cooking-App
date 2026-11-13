using System.Windows.Input;

namespace FridgeChef.Views;

// One row of the Account / Settings / About screens (Figma 8, 19, 22): icon, text, optional value, chevron.
public sealed class SettingsRow : Border
{
    public static readonly BindableProperty IconProperty = BindableProperty.Create(nameof(Icon), typeof(string), typeof(SettingsRow), propertyChanged: (b, _, v) => ((SettingsRow)b)._icon.Source = (string?)v);
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(SettingsRow), propertyChanged: (b, _, v) => ((SettingsRow)b)._text.Text = (string?)v);
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(nameof(Value), typeof(string), typeof(SettingsRow), propertyChanged: (b, _, v) => ((SettingsRow)b)._value.Text = (string?)v);
    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(SettingsRow), propertyChanged: (b, _, v) => ((SettingsRow)b)._tap.Command = (ICommand?)v);
    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(SettingsRow), propertyChanged: (b, _, v) => ((SettingsRow)b)._tap.CommandParameter = v);
    public static readonly BindableProperty IsDangerProperty = BindableProperty.Create(nameof(IsDanger), typeof(bool), typeof(SettingsRow), false, propertyChanged: (b, _, v) => ((SettingsRow)b).ApplyDanger((bool)v));

    readonly Image _icon = new() { WidthRequest = 20, HeightRequest = 20, VerticalOptions = LayoutOptions.Center };
    readonly Label _text = new() { VerticalOptions = LayoutOptions.Center, FontSize = 16 };
    readonly Label _value = new() { VerticalOptions = LayoutOptions.Center, FontSize = 14 };
    readonly Image _chevron = new() { Source = "ic_row_chevron.png", WidthRequest = 18, HeightRequest = 18, VerticalOptions = LayoutOptions.Center };
    readonly TapGestureRecognizer _tap = new();

    public string? Icon { get => (string?)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Value { get => (string?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public bool IsDanger { get => (bool)GetValue(IsDangerProperty); set => SetValue(IsDangerProperty, value); }
    public bool ShowChevron { get => _chevron.IsVisible; set => _chevron.IsVisible = value; }
    public View? Accessory { set { if (value is not null) { _grid.Add(value, 3); _chevron.IsVisible = false; } } } // e.g. a Switch

    readonly Grid _grid = new() { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto)], ColumnSpacing = 14 };

    public SettingsRow()
    {
        SetDynamicResource(StyleProperty, "Card");
        Padding = new Thickness(18, 0);
        HeightRequest = 56;
        _text.SetDynamicResource(Label.FontFamilyProperty, "AppBodyFont");
        _value.SetDynamicResource(Label.FontFamilyProperty, "AppBodyFont");
        _value.SetAppThemeColor(Label.TextColorProperty, C("Olive"), C("OliveLight"));
        ApplyDanger(false);
        _grid.Add(_icon, 0); _grid.Add(_text, 1); _grid.Add(_value, 2); _grid.Add(_chevron, 3);
        Content = _grid;
        GestureRecognizers.Add(_tap);
        SemanticProperties.SetDescription(this, null);
    }

    void ApplyDanger(bool danger)
    {
        if (danger) _text.TextColor = Color.FromArgb("#C0392B");
        else _text.SetAppThemeColor(Label.TextColorProperty, C("Text"), C("TextDark"));
    }

    static Color C(string key) => Application.Current!.Resources.TryGetValue(key, out var v) ? (Color)v : Colors.Gray;
}
