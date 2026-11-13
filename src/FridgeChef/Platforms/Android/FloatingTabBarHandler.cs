using Android.Graphics;
using Android.Views;
using AndroidX.Core.View;
using Google.Android.Material.Navigation;
using Microsoft.Maui.Controls.Handlers;
using Microsoft.Maui.Platform;
using View = Android.Views.View;

namespace FridgeChef;

// Draws the Figma tab bar: floating, rounded, icons only. Its colour comes from Shell.TabBarBackgroundColor (Styles.xaml);
// sizes come from Theme.xaml. The rounding is a clip outline, so MAUI's own colour updates can't undo it.
public class FloatingTabBarHandler : ShellItemHandler
{
	// Called by AppShell after every navigation (MAUI 11 offers no override hook once the bar exists).
	// Safe to run repeatedly: it only sets the same values again.
	public void ApplyFigmaStyle()
	{
		if (BottomNavigationView is not { } bar || bar.Context is not { } ctx) return;
		var r = Application.Current!.Resources;
		T Res<T>(string key) => r.TryGetValue(key, out var v) ? (T)v : default!;
		int Px(double dp) => (int)ctx.ToPixels(dp);

		bar.LabelVisibilityMode = NavigationBarView.LabelVisibilityUnlabeled; // titles stay as screen-reader labels
		bar.ItemActiveIndicatorEnabled = false;
		bar.Elevation = 0; // no shadow (light on low-end phones)
		bar.OutlineProvider = new RoundedOutline(Px(Res<double>("TabBarCornerRadius")));
		bar.ClipToOutline = true;

		// The bar floats above the gesture area, so drop the bottom padding Android adds for it (it pushed icons up).
		ViewCompat.SetOnApplyWindowInsetsListener(bar, null);
		bar.SetPadding(0, 0, 0, 0);

		if (bar.LayoutParameters is ViewGroup.MarginLayoutParams lp)
		{
			var m = Res<Thickness>("TabBarMargin");
			lp.SetMargins(Px(m.Left), Px(m.Top), Px(m.Right), Px(m.Bottom));
			lp.Height = Px(Res<double>("TabBarHeight"));
			bar.LayoutParameters = lp;
		}
	}

	class RoundedOutline(float radius) : ViewOutlineProvider
	{
		public override void GetOutline(View? view, Outline? outline) =>
			outline?.SetRoundRect(0, 0, view?.Width ?? 0, view?.Height ?? 0, radius);
	}
}
