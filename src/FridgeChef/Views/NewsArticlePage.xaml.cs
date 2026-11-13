using FridgeChef.Core.Recipes;

namespace FridgeChef.Views;

public partial class NewsArticlePage : ContentPage, IQueryAttributable
{
	readonly NewsArticleViewModel _vm;

	public NewsArticlePage(NewsArticleViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("item", out var i) && i is NewsItem item) _vm.Item = item;
	}
}
