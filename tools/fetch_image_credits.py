"""Fill image_url / image_credit / image_page for every recipe that has an image_file (a Wikimedia Commons file title).
Usage: python3 -I tools/fetch_image_credits.py   (run from the repo root; rewrites supabase/seed/recipes_*.json)
The credit text comes straight from Commons, so the licence shown in the app is always the real one."""
import glob, json, re, time, urllib.parse, urllib.request

UA = "Mozilla/5.0 (compatible; RecipeSeed/1.0)"

def commons_info(titles):
    url = "https://commons.wikimedia.org/w/api.php?" + urllib.parse.urlencode({
        "action": "query", "format": "json", "titles": "|".join(titles),
        "prop": "imageinfo", "iiprop": "url|extmetadata", "iiurlwidth": 400})
    time.sleep(2)  # be polite: Commons rate-limits
    data = json.load(urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": UA}), timeout=30))
    norm = {n["to"]: n["from"] for n in data["query"].get("normalized", [])}
    out = {}
    for p in data["query"]["pages"].values():
        ii = p["imageinfo"][0]; m = ii["extmetadata"]
        artist = re.sub(r"\s+", " ", re.sub(r"<[^>]+>", "", m.get("Artist", {}).get("value", "Unknown"))).strip()
        lic = m.get("LicenseShortName", {}).get("value", "")
        out[norm.get(p["title"], p["title"])] = {
            "image_url": ii["thumburl"].split("?")[0],  # ~400-500 px wide: small for low-end phones
            "image_credit": f"Photo: {artist[:80]}, {lic}, via Wikimedia Commons",
            "image_page": ii["descriptionurl"]}
    return out

for path in sorted(glob.glob("supabase/seed/recipes_*.json")):
    recipes = json.load(open(path))
    titles = [r["image_file"] for r in recipes if r.get("image_file")]
    info = {}
    for i in range(0, len(titles), 40):
        info.update(commons_info(titles[i:i + 40]))
    for r in recipes:
        if r.get("image_file"):
            r.update(info[r["image_file"]])
    json.dump(recipes, open(path, "w"), ensure_ascii=False, indent=1)
    print(path, len(titles), "images")
