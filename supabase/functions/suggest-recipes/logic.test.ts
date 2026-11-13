// Run: node --test supabase/functions/suggest-recipes/logic.test.ts
import { test } from "node:test";
import assert from "node:assert/strict";
import { cacheKey, parseSuggestions, suggest, validateInput, type Provider } from "./logic.ts";

const ok = (o: unknown) => assert.equal(typeof o, "object", `expected valid input, got: ${o}`);

test("cleans ingredients: trim, lower-case, no duplicates", () => {
  const r = validateInput({ ingredients: [" Eggs ", "eggs", "RICE", "Chicken  breast"], cuisines: ["kh"], language: "en" });
  ok(r);
  assert.deepEqual(r, { ingredients: ["eggs", "rice", "chicken breast"], cuisines: ["KH"], language: "en" });
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
