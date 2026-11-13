-- Fixes from the database review on 13 Nov 2026. Approved by the owner; applied to the live project.
-- Safe to run once: no data is deleted.

-- 1. Security (flagged by Supabase's security advisor): stop logged-out visitors calling internal functions.
revoke execute on function public.handle_new_user() from public, anon, authenticated;  -- only the signup trigger uses it
revoke execute on function public.is_handle_available(text) from public, anon;
grant  execute on function public.is_handle_available(text) to authenticated;          -- Profile setup needs it

-- 2. Brief: recipes, cuisines and news are readable by logged-in users only.
alter policy "cuisines readable" on public.cuisines to authenticated;
alter policy "recipes readable"  on public.recipes  to authenticated;
alter policy "news readable"     on public.news     to authenticated;

-- 3. Usage counts stay on the server (owner: users never see the daily counter).
drop policy "own usage read" on public.ai_usage;

-- 4. Profile photos: JPEG only, max 200 KB (the app resizes to 300 px first).
update storage.buckets set file_size_limit = 204800, allowed_mime_types = '{image/jpeg}' where id = 'avatars';

-- 5. Cuisines: brief's sort_order + flag_emoji; adjective labels for the Fridge page ("Cambodian").
alter table public.cuisines rename column sort to sort_order;
alter table public.cuisines
  add column flag_emoji   text not null default '',
  add column adjective_en text,
  add column adjective_km text;  -- empty = app shows name_km

-- Owner: add Myanmar, skip Thailand.
insert into public.cuisines (code, name_en, name_km) values ('MM', 'Myanmar', 'មីយ៉ាន់ម៉ា')
on conflict (code) do nothing;
update public.cuisines set name_en = 'South Korea', name_km = 'កូរ៉េខាងត្បូង' where code = 'KR';

-- Order from the brief; Cambodia first. GB is kept (not in the brief) at the end until the owner decides.
update public.cuisines c set sort_order = v.ord, flag_emoji = v.flag, adjective_en = v.adj
from (values
  ('KH', 1, '🇰🇭', 'Cambodian'),  ('VN', 3, '🇻🇳', 'Vietnamese'),
  ('LA', 4, '🇱🇦', 'Lao'),        ('MM', 5, '🇲🇲', 'Burmese'),    ('CN', 6, '🇨🇳', 'Chinese'),
  ('JP', 7, '🇯🇵', 'Japanese'),   ('KR', 8, '🇰🇷', 'Korean'),     ('IN', 9, '🇮🇳', 'Indian'),
  ('PH', 10, '🇵🇭', 'Filipino'),  ('ID', 11, '🇮🇩', 'Indonesian'), ('MY', 12, '🇲🇾', 'Malaysian'),
  ('SG', 13, '🇸🇬', 'Singaporean'), ('IT', 14, '🇮🇹', 'Italian'),  ('FR', 15, '🇫🇷', 'French'),
  ('ES', 16, '🇪🇸', 'Spanish'),   ('US', 17, '🇺🇸', 'American'),   ('MX', 18, '🇲🇽', 'Mexican'),
  ('TR', 19, '🇹🇷', 'Turkish'),   ('GR', 20, '🇬🇷', 'Greek'),      ('GB', 99, '🇬🇧', 'British')
) as v(code, ord, flag, adj)
where c.code = v.code;
update public.cuisines set adjective_km = 'ខ្មែរ' where code = 'KH';  -- "Khmer" (food); others read naturally as the country name

-- 6. Recipes: popularity for "Popular now", plus indexes for the Home and Recipe screens.
alter table public.recipes add column popularity int not null default 0;
create index recipes_popularity_idx on public.recipes (popularity desc);
create index recipes_trending_idx   on public.recipes (is_trending) where is_trending;
create index recipes_category_idx   on public.recipes (category);

-- 7. News: one row per article (the hourly refresh upserts by url), newest first.
create unique index news_url_key       on public.news (url);
create index        news_published_idx on public.news (published_at desc);

-- 8. Saved recipes: index the recipe link (speeds up deletes and joins).
create index saved_recipe_idx on public.saved_recipes (recipe_id);
