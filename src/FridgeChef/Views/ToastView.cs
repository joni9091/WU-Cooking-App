using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

// Figma 24 toast: dark brown card with an icon, a title, an optional second line, and an optional action (Undo).
// Every page with toasts puts one ToastView as the last child of its root Grid; ToastService finds it.
public sealed class ToastView : Border
{
    readonly Image _icon = new() { WidthRequest = 20, HeightRequest = 20, VerticalOptions = LayoutOptions.Center };
    readonly Label _title = new() { TextColor = Colors.White, FontSize = 14 };
    readonly Label _detail = new() { TextColor = Color.FromArgb("#D9D9D9"), FontSize = 12 };
    Func<Task>? _action;
    int _shown; // only the newest toast hides itself

    public ToastView()
    {
        IsVisible = false;
        InputTransparent = false;
        VerticalOptions = LayoutOptions.End;
        Margin = new Thickness(16, 0, 16, 104); // above the floating tab bar
        Padding = new Thickness(18, 14);
        StrokeThickness = 0;
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 };
        BackgroundColor = Color.FromArgb("#2B240F");
        _title.SetDynamicResource(Label.FontFamilyProperty, "AppBodyBoldFont");
        _detail.SetDynamicResource(Label.FontFamilyProperty, "AppBodyFont");
        Content = new HorizontalStackLayout { Spacing = 14, Children = { _icon, new VerticalStackLayout { Spacing = 2, Children = { _title, _detail } } } };
        GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => { IsVisible = false; if (_action is { } a) await a(); }) });
        SemanticProperties.SetHeadingLevel(this, SemanticHeadingLevel.None);
    }

    public async void Show(ToastKind kind, string title, string? detail, string? action, Func<Task>? onAction)
    {
        _icon.Source = kind switch
        {
            ToastKind.Success => "ic_check_green.png",
            ToastKind.Offline => "ic_wifi.png",
            ToastKind.Removed => "ic_bookmark_white.png",
            _ => null,
        };
        _icon.IsVisible = _icon.Source is not null;
        _title.Text = title;
        _detail.Text = action ?? detail; // Figma: the action ("Undo") sits on the second line
        _detail.IsVisible = !string.IsNullOrEmpty(_detail.Text);
        _action = onAction;
        SemanticScreenReader.Announce(title);

        var id = ++_shown;
        Opacity = 0;
        IsVisible = true;
        await this.FadeToAsync(1, 150); // short and light for low-end phones
        await Task.Delay(onAction is null ? 2500 : 4000);
        if (id == _shown) IsVisible = false;
    }
}
