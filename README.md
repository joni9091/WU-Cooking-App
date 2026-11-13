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
| `docs/licenses` | Font licences (both fonts are free, SIL Open Font License) |

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

## Run the tests

```
dotnet test tests/FridgeChef.Tests
```

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
