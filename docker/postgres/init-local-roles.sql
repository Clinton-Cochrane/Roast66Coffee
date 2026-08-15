-- Supabase provides these roles automatically. Local PostgreSQL needs inert
-- equivalents so the production RLS migrations can be applied unchanged.
CREATE ROLE anon NOLOGIN;
CREATE ROLE authenticated NOLOGIN;
