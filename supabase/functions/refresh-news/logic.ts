// Pure RSS parsing for refresh-news (no Deno/Supabase APIs, so Node tests can run it).
// shortcut: one source (The Guardian food feed, allowed for personal non-commercial use); add or swap sources before any commercial release.

export const FEED_URL = "https://www.theguardian.com/food/rss";
export const SOURCE = "The Guardian";
export const MAX_SUMMARY = 600;

export type NewsRow = { title_en: string; summary_en: string; source: string; url: string; image_url: string | null; published_at: string };

const ENTITIES: Record<string, string> = { amp: "&", lt: "<", gt: ">", quot: '"', apos: "'", nbsp: " " };

export function decode(s: string): string {
  return s.replace(/&(#x[0-9a-f]+|#\d+|[a-z]+);/gi, (m, e: string) =>
    e[0] === "#" ? String.fromCodePoint(e[1].toLowerCase() === "x" ? parseInt(e.slice(2), 16) : parseInt(e.slice(1), 10)) : ENTITIES[e.toLowerCase()] ?? m);
}

function tag(item: string, name: string): string | undefined {
  const m = item.match(new RegExp(`<${name}(?:\\s[^>]*)?>([\\s\\S]*?)</${name}>`, "i"));
  return m?.[1].replace(/^\s*<!\[CDATA\[([\s\S]*?)\]\]>\s*$/, "$1").trim();
}

// Feed text is escaped HTML: decode, drop tags and the "Continue reading" promo, collapse spaces, cut at a word.
export function cleanSummary(raw: string): string {
  let text = decode(decode(raw)).replace(/<a\b[^>]*>\s*Continue reading[\s\S]*?<\/a>/gi, "");
  text = text.replace(/<\/p>\s*<p>/gi, "\n").replace(/<[^>]+>/g, " ");
  text = text.split("\n").map((p) => p.replace(/\s+/g, " ").trim()).filter(Boolean).join("\n\n");
  if (text.length <= MAX_SUMMARY) return text;
  const cut = text.slice(0, MAX_SUMMARY);
  return cut.slice(0, cut.lastIndexOf(" ")).trimEnd() + "…";
}

const isHttps = (u: string | undefined): u is string => !!u && /^https:\/\/[^\s"'<>]+$/i.test(u);

// Picks the photo closest to 460 px wide (brief: ~400 px for low-end phones).
function image(item: string): string | null {
  const sizes = [...item.matchAll(/<media:content\b[^>]*\bwidth="(\d+)"[^>]*\burl="([^"]+)"/gi)]
    .map((m) => ({ w: Number(m[1]), url: decode(m[2]) }))
    .filter((x) => isHttps(x.url));
  sizes.sort((a, b) => Math.abs(a.w - 460) - Math.abs(b.w - 460));
  return sizes[0]?.url ?? null;
}

export function parseFeed(xml: string): NewsRow[] {
  const rows: NewsRow[] = [];
  for (const [, item] of xml.matchAll(/<item\b[^>]*>([\s\S]*?)<\/item>/gi)) {
    const title = decode(tag(item, "title") ?? "").replace(/\s+/g, " ").trim();
    const url = decode(tag(item, "link") ?? "");
    const date = Date.parse(tag(item, "pubDate") ?? "");
    if (!title || !isHttps(url) || Number.isNaN(date)) continue; // untrusted input: skip anything incomplete
    rows.push({
      title_en: title.slice(0, 300),
      summary_en: cleanSummary(tag(item, "description") ?? ""),
      source: SOURCE,
      url,
      image_url: image(item),
      published_at: new Date(date).toISOString(),
    });
  }
  return rows;
}
