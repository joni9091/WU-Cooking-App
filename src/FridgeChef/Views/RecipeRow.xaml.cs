using FridgeChef.Core.Recipes;
using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

public partial class RecipeRow : Border
{
	public RecipeRow() => InitializeComponent();

	// Cards open the recipe themselves, so every list can reuse them.
	void OnTapped(object? sender, TappedEventArgs e)
	{
		if (BindingContext is Recipe recipe && Handler?.MauiContext?.Services.GetService<IAppNavigator>() is { } nav)
			_ = nav.OpenRecipeAsync(recipe);
	}
}

// "Not" converter: true -> false (e.g. show the placeholder icon when a recipe has no photo).
public sealed class NotConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is not true;
	public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => value is not true;
}
