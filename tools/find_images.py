"""Find free-licence dish photos on Wikimedia Commons.
Usage: python3 -I tools/find_images.py "query1" "query2" ...  -> prints top candidates per query (best licence first).
Only a helper for choosing photos; every chosen photo is still checked by eye."""
import json, re, sys, time, urllib.error, urllib.parse, urllib.request

UA = "Mozilla/5.0 (compatible; RecipeSeed/1.0)"
RANK = lambda lic: 0 if re.match(r"(CC0|Public domain|PD)", lic or "", re.I) else 1 if re.match(r"CC BY \d", lic or "") else 2 if "CC BY-SA" in (lic or "") else 9

def search(q, n=8):
    url = "https://commons.wikimedia.org/w/api.php?" + urllib.parse.urlencode({
        "action": "query", "format": "json", "generator": "search", "gsrnamespace": 6,
        "gsrsearch": q + " filetype:bitmap", "gsrlimit": n, "prop": "imageinfo",
        "iiprop": "url|extmetadata|size|mime", "iiurlwidth": 400})
    for wait in (2, 10, 30, 60):  # Commons rate-limits: back off and retry
        time.sleep(wait)
        try:
            pages = json.load(urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": UA}), timeout=30)).get("query", {}).get("pages", {})
            break
        except urllib.error.HTTPError as e:
            if e.code != 429: raise
    else:
        raise SystemExit("Commons keeps refusing (429); try later")
    out = []
    for p in pages.values():
        ii = p["imageinfo"][0]; m = ii["extmetadata"]
        lic = m.get("LicenseShortName", {}).get("value", "")
        if ii["mime"] != "image/jpeg" or RANK(lic) == 9 or ii["width"] < 600: continue
        artist = re.sub(r"<[^>]+>", "", m.get("Artist", {}).get("value", "")).strip()
        out.append((RANK(lic), p["title"], lic, artist[:40], ii["thumburl"].split("?")[0], ii["descriptionurl"]))
    return sorted(out)[:3]

# Usage: find_images.py OUT.json "query" ...  (appends to OUT.json; prints short summary)
out_path, queries = sys.argv[1], sys.argv[2:]
try: found = json.load(open(out_path))
except FileNotFoundError: found = {}
for q in queries:
    if q in found: continue
    found[q] = [dict(zip(["title", "license", "artist", "thumb", "page"], c[1:])) for c in search(q)]
    json.dump(found, open(out_path, "w"), ensure_ascii=False, indent=1)
    print(f"## {q}: " + " || ".join(f"[{i}] {c['title'][5:60]} ({c['license']})" for i, c in enumerate(found[q])))
