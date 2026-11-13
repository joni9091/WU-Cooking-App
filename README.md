# Fridge Chef

A bilingual (English / Khmer) cooking app for Android, built with .NET MAUI and Supabase.

## Folders

| Folder | What is in it |
|---|---|
| `src/FridgeChef` | The Android app (screens, theme, fonts) |
| `src/FridgeChef.Core` | Shared code that can be unit-tested: all app text (`Resources/AppResources*.resx`), later view models and validation |
| `tests/FridgeChef.Tests` | Unit tests |
| `supabase/migrations` | Database changes, oldest first. Each file runs **once**, in order. The first file is already on the live project |
| `supabase/seed.sql` | Starter recipes and countries (already on the live project) |
| `docs/licenses` | Licences for Kantumruy Pro (SIL OFL) and Lucide icons. Clash Grotesk uses the ITF Free Font License, which comes with its download |

## One-time setup on a Mac

1. Install the .NET 11 SDK from https://dotnet.microsoft.com/download.
2. Install the MAUI tools: `dotnet workload install maui`.
3. Install Java 21 (the Android build needs exactly 21) into your user folder:
   ```
   J=$HOME/Library/Developer/Xamarin/jdk-21; mkdir -p $J
   curl -fL -C - --retry 3 -o /tmp/jdk21.tar.gz https://aka.ms/download-jdk/microsoft-jdk-21-macos-aarch64.tar.gz
   tar -xzf /tmp/jdk21.tar.gz -C $J --strip-components=1   # if java ends up one folder deeper, move it up
   $J/Contents/Home/bin/java -version                       # should say 21
   ```
4. Install the Android platform the app compiles against (API 37):
   ```
   export JAVA_HOME=$HOME/Library/Developer/Xamarin/jdk-21/Contents/Home
   SDK=$HOME/Library/Developer/Xamarin/android-sdk-macosx
   $SDK/cmdline-tools/7.0/bin/sdkmanager --sdk_root=$SDK "platforms;android-37.0" "build-tools;37.0.0" "platform-tools"
   ```
5. Tell the Android build to use Java 21: in `~/.config/xbuild/monodroid-config.xml`, set
   `<java-sdk path="/Users/<you>/Library/Developer/Xamarin/jdk-21/Contents/Home" />`.
   (The warning `XA0033 ... valid version number` during builds is harmless.)

6. Download the English font, Clash Grotesk (free from Fontshare): `./tools/get_fonts.sh`.
   Its licence does not allow the font files in a public repo, so git ignores them. Without this step the app still builds, but English text uses the phone's default font.

The one-step `dotnet build -t:InstallAndroidDependencies` does the same, but it timed out on a slow connection, which is why the manual steps are listed.

## Run on your Android phone

1. On the phone, open Settings, then About phone, and tap **Build number** 7 times to unlock Developer options.
2. In Settings, open Developer options and turn on **USB debugging**.
3. Plug the phone into the Mac with a USB cable and tap **Allow** on the phone.
4. Check that the Mac sees it: `adb devices` (your phone should be listed as `device`).
5. Build, install and start the app:
   ```
   dotnet build src/FridgeChef -t:Run -f net11.0-android
   ```

## Release build (the APK you can share)

The release build is smaller and faster (trimmed and pre-compiled) and is signed with **your own key**.

**The key** lives outside the project, in `~/.fridgechef/` (never in git):
- `fridgechef.keystore`: the signing key.
- `keystore-pass.txt`: its password, written twice on two lines (the signing tool reads one line for the keystore and one for the key).

**Back up this folder** (for example to a USB stick or a private cloud drive). If you lose it, phones that have the app cannot install updates; people would have to uninstall first. If the folder is missing, the build still works but signs with the debug key.

To create a new key on another Mac (only if you have no backup):
```
mkdir -m 700 ~/.fridgechef
P=$(openssl rand -base64 24 | tr -d '/+=\n'); printf '%s\n%s\n' "$P" "$P" > ~/.fridgechef/keystore-pass.txt; unset P
keytool -genkeypair -keystore ~/.fridgechef/fridgechef.keystore -alias fridgechef -keyalg RSA -keysize 2048 -validity 10000 \
  -storepass:file ~/.fridgechef/keystore-pass.txt -keypass:file ~/.fridgechef/keystore-pass.txt -dname "CN=Fridge Chef"
```

Build and install the release APK:
```
dotnet publish src/FridgeChef -f net11.0-android -c Release -o publish
adb install -r publish/com.fridgechef.app-Signed.apk
```
The release APK is for 64-bit ARM phones (nearly every phone since 2017). Before each new release, raise `ApplicationVersion` (and `ApplicationDisplayVersion`) in `src/FridgeChef/FridgeChef.csproj`.

Switching between the debug build (`dotnet build ... -t:Run`) and the release APK on the same phone needs an uninstall first (`adb uninstall com.fridgechef.app`), because they are signed with different keys. Uninstalling logs you out.

## Run the tests

```
dotnet test tests/FridgeChef.Tests                                   # app logic (C#)
node --test supabase/functions/suggest-recipes/logic.test.ts         # recipe-ideas function rules
```

## Sign-in setup (Supabase + Google)

1. **Google Cloud** (free, no billing needed; skip any "free trial" page):
   - Create a project, then in Google Auth Platform set Audience = External, mode **Testing**, and add every tester's Gmail under **Test users** (max 100).
   - Data access (scopes): `openid`, `userinfo.email`, `userinfo.profile`.
   - Clients → Create → **Web application**, Authorized redirect URI: `https://vobwgptmuzichrcvutwq.supabase.co/auth/v1/callback`.
2. **Supabase → Authentication**:
   - Providers → Google: on, paste the Client ID and Client secret (the secret goes only here).
   - Providers → Email: minimum password length `8`.
   - URL Configuration: Site URL `fridgechef://auth-callback`, and add `fridgechef://auth-callback` to Redirect URLs.
   - Email templates → Reset password: include `{{ .Token }}` (the app asks for this code).
3. Supabase's built-in email only reaches members of your Supabase team, a few per hour. Friends should use **Continue with Google** until a custom SMTP service is set up.

## Recipe ideas function (`supabase/functions/suggest-recipes`)

- The app sends `{ ingredients, cuisines, language }` with the user's login; the function checks the input, the daily limit (10), a 7-day cache, then asks **Groq first and NVIDIA once as backup** (owner decision: NVIDIA's free service took 14-53 s).
- Secrets (Supabase → Edge Functions → Secrets): `GROQ_API_KEY`, `GROQ_MODEL`, `NVIDIA_API_KEY`, `NVIDIA_MODEL`, optional `DAILY_LIMIT`. Keys never go in the app or the repo.
- Deploy: it is deployed with **verify_jwt = false** because the function checks the user itself (required with the new publishable/secret keys). With the Supabase CLI: `supabase functions deploy suggest-recipes --no-verify-jwt`.
- Logs: Supabase → Edge Functions → suggest-recipes → Logs. They only contain timings and provider status codes, never ingredients, emails or tokens.

## Text and languages

- Every visible text lives in `src/FridgeChef.Core/Resources/AppResources.resx` (English) and `AppResources.km.resx` (Khmer).
- When you add a text, add it to **both** files. A unit test fails if Khmer is missing a key.
- The app uses the phone's language the first time; a language switch comes in a later phase.

## Design

All colours, fonts and spacing are in `src/FridgeChef/Resources/Styles/Theme.xaml`. To apply the Figma design, change that file only.
The floating tab bar's shape is drawn in `src/FridgeChef/Platforms/Android/FloatingTabBarHandler.cs`, using the sizes from `Theme.xaml`.
The tab icons in `Resources/Images/tab_*.svg` are temporary stand-ins from Lucide (free, ISC licence). Replace them with the Figma exports when available, keeping the same file names.

## Database changes

Apply a new migration file by pasting it into Supabase dashboard → SQL Editor → Run, then note in the file that it is applied.

## Recipes and countries (seed data)

Recipes live in `supabase/seed/recipes_*.json` and countries in `supabase/seed/cuisines.json`. To add or change a recipe:

1. Edit the JSON file. Every recipe needs English **and** Khmer text, with the same number of ingredients and steps in both.
2. For a new photo, set `image_file` to a Wikimedia Commons file name (for example `File:Fish Amok with Rice.jpg`), then run `python3 -I tools/fetch_image_credits.py` to fill in the photo link and credit.
3. Run `python3 -I tools/build_seed.py`. It checks every rule (calories 50–1500, 1–2 sentence descriptions, at least 10 Cambodian and 2 per country…) and writes `supabase/seed.sql`.
4. Copy it with `pbcopy < supabase/seed.sql`, paste it into Supabase → SQL Editor and click Run. It is safe to run again: existing recipes are updated, not duplicated.

Photos come from Wikimedia Commons. Many licences require credit, so `image_credit` must be shown on the recipe detail page.

## Secrets

- Never commit real keys. `.env` is ignored by git; copy `.env.example` to `.env` for local use.
- AI keys (NVIDIA, Groq) live only in Supabase Edge Function secrets (set up in Phase 5).
- The Supabase **publishable** key (in `src/FridgeChef.Core/SupabaseConfig.cs`) may be in the app and the repo: it is public by design. The **service role** key must never be in the app or the repo.

## Git workflow

`main` stays stable. Each task gets its own branch, for example `git switch -c phase-1-setup`.
