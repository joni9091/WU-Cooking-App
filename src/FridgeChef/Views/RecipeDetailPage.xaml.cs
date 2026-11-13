using FridgeChef.Core.Recipes;
using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

public partial class RecipeDetailPage : ContentPage, IQueryAttributable
{
	readonly RecipeDetailViewModel _vm;

	public RecipeDetailPage(RecipeDetailViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	// Receives the recipe passed by AppNavigator.OpenRecipeAsync.
	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("recipe", out var r) && r is Recipe recipe) _vm.Show(recipe);
	}
}
