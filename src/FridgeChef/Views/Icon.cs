namespace FridgeChef.Views;

// Icon.Tint: recolours a one-colour icon (Image or ImageButton). Used with AppThemeBinding so black
// Lucide icons turn light in dark mode where they sit on a dark background. Null = original colours.
public static class Icon
{
    public static readonly BindableProperty TintProperty = BindableProperty.CreateAttached(
        "Tint", typeof(Color), typeof(Icon), null, propertyChanged: (b, _, _) => Watch((View)b));

    public static Color? GetTint(BindableObject view) => (Color?)view.GetValue(TintProperty);
    public static void SetTint(BindableObject view, Color? value) => view.SetValue(TintProperty, value);

    static void Watch(View view)
    {
        view.HandlerChanged -= OnHandlerChanged; // tint again when the native view is (re)created
        view.HandlerChanged += OnHandlerChanged;
        Apply(view);
    }

    static void OnHandlerChanged(object? sender, EventArgs e) => Apply((View)sender!);

    static void Apply(View view)
    {
#if ANDROID
        if (view.Handler?.PlatformView is not Android.Widget.ImageView image) return;
        if (GetTint(view) is { } c)
            image.SetColorFilter(Microsoft.Maui.Platform.ColorExtensions.ToPlatform(c), Android.Graphics.PorterDuff.Mode.SrcIn!);
        else image.ClearColorFilter();
#endif
    }
}
