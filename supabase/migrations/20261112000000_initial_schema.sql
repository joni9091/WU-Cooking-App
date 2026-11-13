-- Fridge Chef database schema (Supabase / Postgres)
-- ALREADY APPLIED to the live project (via the dashboard). Do not run again; add new migration files instead.

-- ========== TABLES ==========
create table public.profiles (
  id uuid primary key references auth.users(id) on delete cascade,
  handle text unique check (handle ~ '^[a-z0-9._]{3,20}$'),
  display_name text check (char_length(display_name) between 1 and 40),
  avatar_url text,
  language text not null default 'en' check (language in ('en','km')),
  created_at timestamptz not null default now()
);

create table public.cuisines (
  code text primary key,
  name_en text not null,
  name_km text not null,
  sort int not null default 100
);

create table public.recipes (
  id uuid primary key default gen_random_uuid(),
  title_en text not null,
  title_km text not null,
  description_en text not null,
  description_km text not null,
  calories int not null check (calories between 50 and 1500),  -- per serving
  cuisine text not null references public.cuisines(code),
  category text not null default 'main',
  prep_minutes int not null default 30,
  servings int not null default 2,
  difficulty text not null default 'easy' check (difficulty in ('easy','medium','hard')),
  image_url text,
  ingredients_en jsonb not null default '[]',
  ingredients_km jsonb not null default '[]',
  steps_en jsonb not null default '[]',
  steps_km jsonb not null default '[]',
  is_trending boolean not null default false,
  is_published boolean not null default true,
  created_at timestamptz not null default now()
);
create index recipes_cuisine_idx on public.recipes(cuisine);

create table public.saved_recipes (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  recipe_id uuid references public.recipes(id) on delete cascade,
  ai_recipe jsonb,                       -- saved AI suggestion (no row in recipes)
  created_at timestamptz not null default now(),   -- stored in UTC, app shows e.g. 2 Dec 2026
  check (recipe_id is not null or ai_recipe is not null),
  unique (user_id, recipe_id)
);
create index saved_user_idx on public.saved_recipes(user_id, created_at desc);

create table public.ai_cache (
  cache_key text primary key,            -- sorted ingredients + sorted cuisines + language
  result jsonb not null,
  created_at timestamptz not null default now()
);

create table public.ai_usage (
  user_id uuid not null references auth.users(id) on delete cascade,
  day date not null default (now() at time zone 'utc')::date,
  count int not null default 0,
  primary key (user_id, day)
);

create table public.news (
  id uuid primary key default gen_random_uuid(),
  title_en text not null,
  title_km text,
  summary_en text,
  summary_km text,
  body_en text,
  body_km text,
  source text,
  url text,
  image_url text,
  published_at timestamptz not null default now(),
  is_published boolean not null default true
);

-- ========== ROW LEVEL SECURITY ==========
alter table public.profiles      enable row level security;
alter table public.cuisines      enable row level security;
alter table public.recipes       enable row level security;
alter table public.saved_recipes enable row level security;
alter table public.ai_cache      enable row level security;  -- no policies: only the Edge Function (service role) can use it
alter table public.ai_usage      enable row level security;
alter table public.news          enable row level security;

create policy "own profile read"   on public.profiles for select to authenticated using (id = auth.uid());
create policy "own profile insert" on public.profiles for insert to authenticated with check (id = auth.uid());
create policy "own profile update" on public.profiles for update to authenticated using (id = auth.uid()) with check (id = auth.uid());

create policy "cuisines readable" on public.cuisines for select to anon, authenticated using (true);
create policy "recipes readable"  on public.recipes  for select to anon, authenticated using (is_published);
create policy "news readable"     on public.news     for select to anon, authenticated using (is_published);

create policy "own saved read"   on public.saved_recipes for select to authenticated using (user_id = auth.uid());
create policy "own saved insert" on public.saved_recipes for insert to authenticated with check (user_id = auth.uid());
create policy "own saved delete" on public.saved_recipes for delete to authenticated using (user_id = auth.uid());

create policy "own usage read" on public.ai_usage for select to authenticated using (user_id = auth.uid());

-- ========== FUNCTIONS ==========
-- Create an empty profile row when someone signs up (handle is picked later in Profile setup)
create function public.handle_new_user() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  insert into public.profiles (id) values (new.id) on conflict do nothing;
  return new;
end $$;
create trigger on_auth_user_created after insert on auth.users
  for each row execute function public.handle_new_user();

-- Live check for the @handle field
create function public.is_handle_available(h text) returns boolean
language sql security definer set search_path = public stable as $$
  select not exists (select 1 from public.profiles where handle = lower(h));
$$;
grant execute on function public.is_handle_available(text) to authenticated;

-- Used by the suggest-recipes Edge Function: add 1 try, return how many are left (-1 = limit reached)
create function public.ai_try_consume(p_user uuid, p_limit int default 10) returns int
language plpgsql security definer set search_path = public as $$
declare used int;
begin
  insert into public.ai_usage (user_id, day, count) values (p_user, (now() at time zone 'utc')::date, 0)
    on conflict do nothing;
  update public.ai_usage set count = count + 1
    where user_id = p_user and day = (now() at time zone 'utc')::date and count < p_limit
    returning count into used;
  if used is null then return -1; end if;
  return p_limit - used;
end $$;
revoke execute on function public.ai_try_consume(uuid,int) from public, anon, authenticated;

-- ========== STORAGE (profile photos) ==========
insert into storage.buckets (id, name, public) values ('avatars','avatars',true) on conflict do nothing;
create policy "avatars public read" on storage.objects for select using (bucket_id = 'avatars');
create policy "avatars own upload" on storage.objects for insert to authenticated
  with check (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth.uid()::text);
create policy "avatars own update" on storage.objects for update to authenticated
  using (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth.uid()::text);
create policy "avatars own delete" on storage.objects for delete to authenticated
  using (bucket_id = 'avatars' and (storage.foldername(name))[1] = auth.uid()::text);
