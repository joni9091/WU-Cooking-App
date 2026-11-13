// Run: node --test supabase/functions/suggest-recipes/logic.test.ts
import { test } from "node:test";
import assert from "node:assert/strict";
import { buildMessages, cacheKey, parseSuggestions, suggest, validateInput, type Input, type Provider } from "./logic.ts";

const ok = (o: unknown) => assert.equal(typeof o, "object", `expected valid input, got: ${o}`);

test("cleans ingredients: trim, lower-case, no duplicates", () => {
  const r = validateInput({ ingredients: [" Eggs ", "eggs", "RICE", "Chicken  breast"], cuisines: ["kh"], language: "en" });
  ok(r);
  assert.deepEqual(r, { ingredients: ["eggs", "rice", "chicken breast"], cuisines: ["KH"], language: "en", exclude: [] });
});

test("accepts Khmer ingredients", () => ok(validateInput({ ingredients: ["ស៊ុត", "បាយ"], cuisines: [], language: "km" })));

test("rejects bad input", () => {
  const bad = [
    { ingredients: ["eggs"], cuisines: [], language: "en" },                         // only 1 item
    { ingredients: ["eggs", "eggs "], cuisines: [], language: "en" },                // 1 after removing duplicates
    { ingredients: Array.from({ length: 21 }, (_, i) => `item${i}`), cuisines: [], language: "en" },
    { ingredients: ["eggs", "x".repeat(41)], cuisines: [], language: "en" },        // too long
    { ingredients: ["eggs", "rice; DROP TABLE"], cuisines: [], language: "en" },    // ';' not allowed
    { ingredients: ["eggs", "<script>"], cuisines: [], language: "en" },
    { ingredients: ["eggs", "rice"], cuisines: ["KH", "VN", "LA", "CN"], language: "en" }, // 4 cuisines
    { ingredients: ["eggs", "rice"], cuisines: ["Cambodia"], language: "en" },      // not a code
    { ingredients: ["eggs", "rice"], cuisines: [], language: "fr" },
    { ingredients: ["eggs", 5], cuisines: [], language: "en" },
    null,
  ];
  for (const b of bad) assert.equal(typeof validateInput(b), "string", JSON.stringify(b));
});

test("cache key ignores order and case", async () => {
  const a = await cacheKey({ ingredients: ["eggs", "rice"], cuisines: ["VN", "KH"], language: "en" });
  const b = await cacheKey({ ingredients: ["rice", "eggs"], cuisines: ["KH", "VN"], language: "en" });
  const c = await cacheKey({ ingredients: ["rice", "eggs"], cuisines: ["KH", "VN"], language: "km" });
  assert.equal(a, b);
  assert.notEqual(a, c);
});

const recipe = (over: Record<string, unknown> = {}) => ({
  name: "Fried rice", description: "Quick rice.", calories_per_serving: 420, cook_time_min: 20, servings: 2,
  have: ["2 eggs"], missing: [], steps: ["Heat oil.", "Fry."], ...over,
});
const answer = (recipes: unknown) => JSON.stringify({ recipes });

test("accepts a valid answer", () => assert.equal(parseSuggestions(answer([recipe(), recipe(), recipe()]))?.length, 3));

test("rejects wrong answers", () => {
  assert.equal(parseSuggestions("not json"), null);
  assert.equal(parseSuggestions(answer([recipe(), recipe()])), null);                                   // not 3
  assert.equal(parseSuggestions(answer([recipe(), recipe(), recipe({ calories_per_serving: 2000 })])), null);
  assert.equal(parseSuggestions(answer([recipe(), recipe(), recipe({ calories_per_serving: 420.5 })])), null);
  assert.equal(parseSuggestions(answer([recipe(), recipe(), recipe({ steps: Array(9).fill("x") })])), null);
  assert.equal(parseSuggestions(answer([recipe(), recipe(), recipe({ name: "" })])), null);
});

const providers: Provider[] = [
  { name: "first", url: "https://first.test", key: "k", model: "m", extra: {} },
  { name: "backup", url: "https://backup.test", key: "k", model: "m", extra: {} },
];
const reply = (status: number, content?: string) =>
  new Response(JSON.stringify({ choices: [{ message: { content } }] }), { status });
const good = answer([recipe(), recipe(), recipe()]);

test("uses the first provider when it works", async () => {
  const calls: string[] = [];
  const r = await suggest(providers, [], async (url) => { calls.push(url); return reply(200, good); });
  assert.equal(r?.length, 3);
  assert.deepEqual(calls, ["https://first.test"]);
});

for (const [why, first] of [
  ["429", () => reply(429)], ["500", () => reply(500)], ["invalid answer", () => reply(200, "{}")],
  ["timeout", () => new Promise<Response>(() => {})],
] as const) {
  test(`after ${why}, exactly one backup call`, async () => {
    const calls: string[] = [];
    const r = await suggest(providers, [], async (url, init) => {
      calls.push(url);
      if (url.includes("first")) {
        const p = (first as () => Promise<Response> | Response)();
        return await Promise.race([p, new Promise<Response>((_, no) => init.signal?.addEventListener("abort", () => no(new DOMException("timeout", "TimeoutError"))))]);
      }
      return reply(200, good);
    }, 50);
    assert.equal(r?.length, 3);
    assert.deepEqual(calls, ["https://first.test", "https://backup.test"]);
  });
}

test("both fail: no third call, result is null (AI_BUSY)", async () => {
  let calls = 0;
  const r = await suggest(providers, [], async () => { calls++; return reply(503); });
  assert.equal(r, null);
  assert.equal(calls, 2);
});

test("missing secret counts as a failure, not a crash", async () => {
  const r = await suggest([{ ...providers[0], key: undefined }, providers[1]], [], async () => reply(200, good));
  assert.equal(r?.length, 3);
});

test("More ideas: exclude list is cleaned, capped and never an error", () => {
  const r = validateInput({ ingredients: ["eggs", "rice"], language: "en",
    exclude: ["  Egg  Fried Rice ", "Egg Fried Rice", 5, "", "x".repeat(81), "bad{json}", ...Array.from({ length: 20 }, (_, i) => `Dish ${i}`)] }) as Input;
  assert.equal(typeof r, "object");
  assert.equal(r.exclude![0], "Egg Fried Rice");
  assert.ok(!r.exclude!.includes("bad{json}"));
  assert.equal(r.exclude!.length, 12);
  assert.equal(typeof validateInput({ ingredients: ["eggs", "rice"], language: "en", exclude: "Pho" }), "string");
});

test("More ideas: a different cache entry, and the dishes go to the provider as data", async () => {
  const base: Input = { ingredients: ["eggs", "rice"], cuisines: [], language: "en" };
  const first = await cacheKey(base);
  assert.equal(first, await cacheKey({ ...base, exclude: [] })); // old cached answers still match
  const more = await cacheKey({ ...base, exclude: ["Omelette", "Fried Rice"] });
  assert.notEqual(first, more);
  assert.equal(more, await cacheKey({ ...base, exclude: ["fried rice", "OMELETTE"] }));
  const user = JSON.parse(buildMessages({ ...base, exclude: ["Omelette"] }, [])[1].content);
  assert.deepEqual(user.avoid_dishes, ["Omelette"]);
  assert.equal(JSON.parse(buildMessages(base, [])[1].content).avoid_dishes, undefined);
});
