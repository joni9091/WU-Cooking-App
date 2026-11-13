#!/bin/sh
# Downloads Clash Grotesk (free, ITF Free Font License) from Fontshare into the app.
# Its licence forbids sharing the font files publicly, so they are NOT in git: run this once after cloning.
set -e
dir="$(cd "$(dirname "$0")/.." && pwd)/src/FridgeChef/Resources/Fonts"
tmp="$(mktemp -d)"
curl -fsSL -o "$tmp/clash.zip" "https://api.fontshare.com/v2/fonts/download/clash-grotesk"
unzip -q "$tmp/clash.zip" -d "$tmp"
for w in Regular Medium Semibold; do
  cp "$tmp"/ClashGrotesk_Complete/Fonts/WEB/fonts/ClashGrotesk-$w.ttf "$dir/"
done
rm -rf "$tmp"
echo "Clash Grotesk copied to $dir"
