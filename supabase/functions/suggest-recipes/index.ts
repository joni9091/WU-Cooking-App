// suggest-recipes: the app sends { ingredients, cuisines, language } with the user's login token and gets 3 recipe ideas.
// Deploy with verify_jwt = false: the platform check doesn't understand the new key types, so the user is verified below.
import { createClient } from "npm:@supabase/supabase-js@2";
import { buildMessages, cacheKey, type Provider, suggest, validateInput } from "./logic.ts";

const DAILY_LIMIT = Number(Deno.env.get("DAILY_LIMIT") ?? 10);
const CACHE_DAYS = 7;

// Owner decision: Groq first (fast), NVIDIA once as backup. Model names and keys come from function secrets.
const PROVIDERS: Provider[] = [
  {
    name: "groq", url: "https://api.groq.com/openai/v1/chat/completions",
    key: Deno.env.get("GROQ_API_KEY"), model: Deno.env.get("GROQ_MODEL"), extra: { reasoning_effort: "low" },
  },
  {
    name: "nvidia", url: "https://integrate.api.nvidia.com/v1/chat/completions",
    key: Deno.env.get("NVIDIA_API_KEY"), model: Deno.env.get("NVIDIA_MODEL"), extra: { chat_template_kwargs: { enable_thinking: false } },
  },
];

const secretKey = (() => {
  try { return JSON.parse(Deno.env.get("SUPABASE_SECRET_KEYS") ?? "{}")["default"]; } catch { return undefined; }
})() ?? Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const admin = createClient(Deno.env.get("SUPABASE_URL")!, secretKey, { auth: { persistSession: false } });

const reply = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

Deno.serve(async (req) => {
  if (req.method !== "POST") return reply(405, { error: "METHOD_NOT_ALLOWED" });

  // 1. Logged-in users only.
  const token = req.headers.get("Authorization")?.replace(/^Bearer\s+/i, "");
  const { data: auth } = token ? await admin.auth.getUser(token) : { data: { user: null } };
  const user = auth?.user;
  if (!user) return reply(401, { error: "UNAUTHORIZED" });

  // 2. Validate (also re-checked here even though the app checks first).
  let body: unknown;
  try { body = await req.json(); } catch { return reply(400, { error: "INVALID_INPUT", message: "body must be JSON" }); }
  const input = validateInput(body);
  if (typeof input === "string") return reply(400, { error: "INVALID_INPUT", message: input });

  let cuisineNames: string[] = [];
  if (input.cuisines.length > 0) {
    const { data, error } = await admin.from("cuisines").select("code,name_en").in("code", input.cuisines);
    if (error) return reply(500, { error: "SERVER_ERROR" });
    if ((data ?? []).length !== input.cuisines.length) return reply(400, { error: "INVALID_INPUT", message: "unknown cuisine" });
    cuisineNames = data!.map((c) => c.name_en);
  }

  // 3. Daily limit per user (UTC day).
  const today = new Date().toISOString().slice(0, 10);
  const { data: usage } = await admin.from("ai_usage").select("count").eq("user_id", user.id).eq("day", today).maybeSingle();
  if ((usage?.count ?? 0) >= DAILY_LIMIT) return reply(429, { error: "RATE_LIMIT_USER", limit: DAILY_LIMIT });

  // 4. Same fridge asked in the last 7 days: answer from the cache, free of charge.
  const key = await cacheKey(input);
  const since = new Date(Date.now() - CACHE_DAYS * 86400_000).toISOString();
  const { data: cached } = await admin.from("ai_cache").select("result").eq("cache_key", key).gte("created_at", since).maybeSingle();
  if (cached) return reply(200, { recipes: cached.result, cached: true });

  // 5-7. Ask the providers (first, then exactly one backup), with the answer checked against the schema.
  const started = Date.now();
  const recipes = await suggest(PROVIDERS, buildMessages(input, cuisineNames), fetch);
  console.log(`suggest: ${recipes ? "ok" : "busy"} in ${Date.now() - started} ms`); // no user data in logs
  if (!recipes) return reply(503, { error: "AI_BUSY" });

  // 8. Remember the answer and count the try (only successful, non-cached answers count).
  await admin.from("ai_cache").upsert({ cache_key: key, result: recipes, created_at: new Date().toISOString() });
  await admin.rpc("ai_try_consume", { p_user: user.id, p_limit: DAILY_LIMIT });
  return reply(200, { recipes, cached: false });
});
