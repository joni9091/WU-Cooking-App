using Android.App;
using Android.Content;
using Android.Content.PM;

namespace FridgeChef;

// Receives fridgechef://auth-callback after Google sign-in and hands it back to WebAuthenticator.
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "fridgechef", DataHost = "auth-callback")]
public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
