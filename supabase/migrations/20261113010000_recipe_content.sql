-- Phase 2: columns needed for the recipe content. Additive only (no data removed). Applied to the live project.

-- Photo credit: most photos are CC BY / CC BY-SA, which require showing the author and licence (detail page, Phase 4).
alter table public.recipes
  add column image_credit text,   -- e.g. "Photo: Jane Doe, CC BY 2.0, via Wikimedia Commons"
  add column image_page   text;   -- source page with full licence details

-- One row per dish name, so seed.sql can be re-run safely (insert ... on conflict update).
alter table public.recipes add constraint recipes_title_en_key unique (title_en);

-- Fixed category list; matches the Recipe tab chips (All, Soup, Rice, Sweet, Drinks, ...).
alter table public.recipes add constraint recipes_category_check
  check (category in ('main', 'soup', 'rice', 'noodles', 'curry', 'salad', 'snack', 'sweet', 'drinks'));
