// delete-account: removes the signed-in user's photo files and auth account. Their profile, saved recipes and usage rows
// go with it (foreign keys "on delete cascade"). Only ever deletes the caller. Deployed with verify_jwt = false: user checked here.
import { createClient } from "npm:@supabase/supabase-js@2";

const secretKey = (() => {
  try { return JSON.parse(Deno.env.get("SUPABASE_SECRET_KEYS") ?? "{}")["default"]; } catch { return undefined; }
})() ?? Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const admin = createClient(Deno.env.get("SUPABASE_URL")!, secretKey, { auth: { persistSession: false } });

const reply = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

Deno.serve(async (req) => {
  if (req.method !== "POST") return reply(405, { error: "METHOD_NOT_ALLOWED" });
  const token = req.headers.get("Authorization")?.replace(/^Bearer\s+/i, "");
  const { data } = token ? await admin.auth.getUser(token) : { data: { user: null } };
  const user = data?.user;
  if (!user) return reply(401, { error: "UNAUTHORIZED" });

  // Profile photo files live in avatars/<user id>/.
  const { data: files } = await admin.storage.from("avatars").list(user.id);
  if (files?.length) await admin.storage.from("avatars").remove(files.map((f) => `${user.id}/${f.name}`));

  const { error } = await admin.auth.admin.deleteUser(user.id);
  if (error) { console.error(`delete-account: failed (${error.status})`); return reply(500, { error: "DELETE_FAILED" }); }
  console.log("delete-account: done"); // no ids or emails in logs
  return reply(200, { deleted: true });
});
