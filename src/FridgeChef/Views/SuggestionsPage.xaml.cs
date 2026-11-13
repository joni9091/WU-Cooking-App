using FridgeChef.Core.Recipes;
using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

public partial class SuggestionsPage : ContentPage, IQueryAttributable
{
	readonly SuggestionsViewModel _vm;

	public SuggestionsPage(SuggestionsViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	// Started by AppNavigator.OpenSuggestionsAsync with the fridge contents.
	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("request", out var r) && r is SuggestRequest request && query.TryGetValue("subtitle", out var s))
			_ = _vm.StartAsync(request, (string)s);
	}

	// Android back while loading = Cancel.
	protected override bool OnBackButtonPressed()
	{
		_vm.CancelCommand.Execute(null);
		return true;
	}
}
