using FridgeChef.Core.ViewModels;

namespace FridgeChef.Views;

// Shown as a modal sheet; Result completes with the chosen codes (Apply) or null (tap outside / back).
public partial class CuisinePickerPage : ContentPage
{
	readonly CuisinePickerViewModel _vm;
	readonly TaskCompletionSource<IReadOnlyList<string>?> _result = new();

	public Task<IReadOnlyList<string>?> Result => _result.Task;

	public CuisinePickerPage(CuisinePickerViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	async void OnApply(object? sender, EventArgs e) => await CloseAsync(_vm.SelectedCodes);
	async void OnCancel(object? sender, TappedEventArgs e) => await CloseAsync(null);

	protected override bool OnBackButtonPressed()
	{
		_ = CloseAsync(null);
		return true;
	}

	async Task CloseAsync(IReadOnlyList<string>? codes)
	{
		if (_result.Task.IsCompleted) return;
		_result.SetResult(codes);
		await Navigation.PopModalAsync(animated: false);
	}
}
