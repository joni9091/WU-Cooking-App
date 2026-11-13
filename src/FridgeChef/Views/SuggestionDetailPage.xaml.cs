using FridgeChef.Core.Recipes;
using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

public partial class SuggestionDetailPage : ContentPage, IQueryAttributable
{
	readonly SuggestionDetailViewModel _vm;

	public SuggestionDetailPage(SuggestionDetailViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("suggestion", out var s) && s is Suggestion suggestion) _vm.Show(suggestion);
	}
}
