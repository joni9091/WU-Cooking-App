using FridgeChef.Core;
using FridgeChef.Core.Resources;

namespace FridgeChef.Views;

public partial class AboutPage : ContentPage
{
	public AboutPage()
	{
		InitializeComponent();
		VersionLabel.Text = string.Format(AppResources.Version, Format.Digits(AppInfo.Current.VersionString));
		TermsRow.Command = new Command(() => Open(AppResources.Terms, AppResources.TermsText));
		PrivacyRow.Command = new Command(() => Open(AppResources.Privacy, string.Format(AppResources.PrivacyText, AppContact.Email)));
		LicensesRow.Command = new Command(() => Open(AppResources.Licenses, AppResources.LicensesText));
		// Opens the phone's mail app; nothing is sent automatically.
		ContactRow.Command = new Command(async () => await Launcher.Default.OpenAsync(new Uri($"mailto:{AppContact.Email}?subject=Fridge%20Chef")));
	}

	void Open(string title, string text) => _ = Navigation.PushAsync(new LegalTextPage(title, text));

	void OnBack(object? s, TappedEventArgs e) => _ = Navigation.PopAsync();
}

// Terms / Privacy / Licenses: a title and paragraphs.
public sealed class LegalTextPage : ContentPage
{
	public LegalTextPage(string title, string text)
	{
		Shell.SetTabBarIsVisible(this, false);
		var back = new Image { Source = "ic_chevron_left.png", WidthRequest = 24, HeightRequest = 24, VerticalOptions = LayoutOptions.Center };
		back.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await Navigation.PopAsync()) });
		SemanticProperties.SetDescription(back, AppResources.Back);
		var heading = new Label { Text = title, FontSize = 28 };
		heading.SetDynamicResource(StyleProperty, "Title");
		var body = new VerticalStackLayout { Spacing = 14 };
		foreach (var p in text.Split("\n\n")) body.Add(new Label { Text = p, FontSize = 15 });
		Content = new ScrollView
		{
			Content = new VerticalStackLayout
			{
				Padding = new Thickness(31, 62, 31, 40), Spacing = 16,
				Children = { new HorizontalStackLayout { Spacing = 12, Children = { back, heading } }, body },
			},
		};
	}
}
