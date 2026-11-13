// Run: node --test supabase/functions/refresh-news/logic.test.ts
import { test } from "node:test";
import assert from "node:assert/strict";
import { cleanSummary, decode, MAX_SUMMARY, parseFeed } from "./logic.ts";

// Shaped like the real Guardian feed: escaped HTML description, &amp; in image URLs, several image widths.
const FEED = `<?xml version="1.0"?><rss><channel>
<item>
  <title>Why Khmer fish amok is winning hearts &amp; minds</title>
  <link>https://www.theguardian.com/food/2026/nov/12/fish-amok</link>
  <description>&lt;p&gt;Chefs love the &amp;quot;steamed&amp;quot; curry.&lt;/p&gt;&lt;p&gt;Home cooks too.&lt;/p&gt; &lt;a href="https://x"&gt;Continue reading...&lt;/a&gt;</description>
  <pubDate>Thu, 12 Nov 2026 09:00:00 GMT</pubDate>
  <media:content width="140" url="https://i.guim.co.uk/a.jpg?width=140&amp;quality=85&amp;s=1"/>
  <media:content width="460" url="https://i.guim.co.uk/a.jpg?width=460&amp;quality=85&amp;s=2"/>
  <media:content width="700" url="https://i.guim.co.uk/a.jpg?width=700&amp;quality=85&amp;s=3"/>
</item>
<item><title>No link</title><pubDate>Thu, 12 Nov 2026 09:00:00 GMT</pubDate></item>
<item><title>Bad link</title><link>javascript:alert(1)</link><pubDate>Thu, 12 Nov 2026 09:00:00 GMT</pubDate></item>
<item><title>Plain http</title><link>http://example.com/x</link><pubDate>Thu, 12 Nov 2026 09:00:00 GMT</pubDate></item>
<item><title>No date</title><link>https://example.com/y</link></item>
<item><title><![CDATA[CDATA title]]></title><link>https://example.com/z</link><pubDate>Wed, 11 Nov 2026 09:00:00 GMT</pubDate></item>
</channel></rss>`;

test("keeps only complete, https items", () => {
  const rows = parseFeed(FEED);
  assert.deepEqual(rows.map((r) => r.title_en), ["Why Khmer fish amok is winning hearts & minds", "CDATA title"]);
});

test("decodes the image link and picks the ~460 px one", () => {
  assert.equal(parseFeed(FEED)[0].image_url, "https://i.guim.co.uk/a.jpg?width=460&quality=85&s=2");
});

test("summary is plain text without the promo link", () => {
  assert.equal(parseFeed(FEED)[0].summary_en, 'Chefs love the "steamed" curry.\n\nHome cooks too.');
});

test("dates become ISO UTC", () => assert.equal(parseFeed(FEED)[0].published_at, "2026-11-12T09:00:00.000Z"));

test("long summaries are cut at a word", () => {
  const s = cleanSummary("&lt;p&gt;" + "word ".repeat(300) + "&lt;/p&gt;");
  assert.ok(s.length <= MAX_SUMMARY + 1 && s.endsWith("…"));
});

test("numeric entities decode", () => assert.equal(decode("&#8216;Hi&#x2019;"), "‘Hi’"));

test("a broken feed gives no rows (existing news is kept)", () => assert.deepEqual(parseFeed("<html>error</html>"), []));
