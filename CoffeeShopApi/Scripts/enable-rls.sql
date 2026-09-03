-- Enable Row Level Security (RLS) on all public tables.
-- With RLS enabled and no policies, PostgreSQL denies access to non-owner roles.
-- The .NET API connects as the table owner and therefore retains direct access.

ALTER TABLE public."__EFMigrationsHistory" ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.notificationsettings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.orders ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.addons ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.menuitems ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.orderitems ENABLE ROW LEVEL SECURITY;

-- Do not add provider-specific roles or policies; deployment must work on stock PostgreSQL.
