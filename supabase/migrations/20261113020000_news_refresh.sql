-- Phase 6: hourly news refresh.

-- One row that remembers the last fetch. refresh-news claims it atomically (update ... where last_run < now() - 55 min),
-- so the feed is fetched at most once an hour even if the function is called more often. Only the function uses it.
create table public.news_refresh (
  id int primary key default 1 check (id = 1),
  last_run timestamptz not null default '1970-01-01'
);
alter table public.news_refresh enable row level security;  -- no policies: server only
insert into public.news_refresh default values;

-- Timer + HTTP from Postgres (both free on Supabase); the schedule itself is created after the function is deployed.
create extension if not exists pg_cron;
create extension if not exists pg_net;

-- Applied after deploying refresh-news (migration "news_schedule"): call it at minute 5 of every hour.
select cron.schedule(
  'refresh-news-hourly',
  '5 * * * *',
  $$ select net.http_post(
       url := 'https://vobwgptmuzichrcvutwq.supabase.co/functions/v1/refresh-news',
       headers := '{"Content-Type": "application/json"}'::jsonb,
       body := '{}'::jsonb,
       timeout_milliseconds := 30000) $$
);
