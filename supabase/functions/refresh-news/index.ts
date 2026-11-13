// refresh-news: fetches the food news feed into the news table. Called hourly by pg_cron (see migrations).
// verify_jwt = false and no secret: anyone may call it, but the news_refresh claim below caps real work at once an hour.
import { createClient } from "npm:@supabase/supabase-js@2";
import { FEED_URL, parseFeed } from "./logic.ts";

const secretKey = (() => {
  try { return JSON.parse(Deno.env.get("SUPABASE_SECRET_KEYS") ?? "{}")["default"]; } catch { return undefined; }
})() ?? Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const admin = createClient(Deno.env.get("SUPABASE_URL")!, secretKey, { auth: { persistSession: false } });

const reply = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

Deno.serve(async () => {
  // Claim this hour's run in one statement; if someone else ran it in the last 55 minutes, do nothing.
  const now = new Date();
  const { data: claimed } = await admin.from("news_refresh")
    .update({ last_run: now.toISOString() })
    .eq("id", 1).lt("last_run", new Date(now.getTime() - 55 * 60_000).toISOString())
    .select("id");
  if (!claimed?.length) { console.log("news: skipped (ran less than an hour ago)"); return reply(200, { skipped: true }); }

  let rows;
  try {
    const res = await fetch(FEED_URL, { signal: AbortSignal.timeout(15000), headers: { "User-Agent": "Mozilla/5.0 (compatible; NewsReader/1.0)" } });
    if (!res.ok) throw new Error(`feed http ${res.status}`);
    rows = parseFeed(await res.text());
  } catch (e) {
    console.error(`news: fetch failed (${(e as Error).message})`); // existing news stays as it is
    return reply(502, { error: "FEED_UNAVAILABLE" });
  }
  if (rows.length === 0) { console.error("news: feed had no usable items"); return reply(502, { error: "FEED_EMPTY" }); }

  const { error } = await admin.from("news").upsert(rows, { onConflict: "url" });
  if (error) { console.error(`news: save failed (${error.code})`); return reply(500, { error: "SAVE_FAILED" }); }
  await admin.from("news").delete().lt("published_at", new Date(now.getTime() - 30 * 86400_000).toISOString()); // keep 30 days
  console.log(`news: saved ${rows.length} items`);
  return reply(200, { saved: rows.length });
});
