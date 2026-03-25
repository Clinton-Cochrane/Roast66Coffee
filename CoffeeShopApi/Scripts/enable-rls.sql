-- Enable Row Level Security (RLS) on all public tables
-- Supabase security recommendation: RLS must be enabled on tables exposed to PostgREST
--
-- With RLS enabled and no policies, PostgREST (anon/authenticated) access is blocked.
-- The .NET API uses a direct PostgreSQL connection (postgres role) which bypasses RLS.
--
-- Run this in Supabase SQL Editor if you prefer manual execution over EF migrations.

ALTER TABLE public."__EFMigrationsHistory" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.notificationsettings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.orders ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.addons ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.menuitems ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.orderitems ENABLE ROW LEVEL SECURITY;

-- Supabase "RLS enabled No Policy": add explicit deny policies for anon/authenticated.
-- Prefer applying these via EF migration AddRlsPoliciesAndDropUnusedIndexes (CoffeeShopApi/Migrations).
-- Example (one table):
-- CREATE POLICY "Deny_supabase_client_access" ON public.orders
--   FOR ALL TO anon, authenticated USING (false) WITH CHECK (false);
