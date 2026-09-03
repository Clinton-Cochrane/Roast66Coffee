-- Enable Row Level Security (RLS) on every public table owned by this database role.
-- With RLS enabled and no policies, PostgreSQL denies access to non-owner roles.
-- The .NET API connects as the table owner and therefore retains direct access.

DO $enable_rls$
DECLARE
    api_table record;
BEGIN
    FOR api_table IN
        SELECT relation.relname
        FROM pg_class AS relation
        INNER JOIN pg_namespace AS schema
            ON schema.oid = relation.relnamespace
        WHERE schema.nspname = 'public'
          AND relation.relkind IN ('r', 'p')
          AND relation.relowner = (
              SELECT role.oid
              FROM pg_roles AS role
              WHERE role.rolname = current_user)
    LOOP
        EXECUTE format(
            'ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY',
            api_table.relname);
    END LOOP;
END
$enable_rls$;

-- Do not add provider-specific roles or policies; deployment must work on stock PostgreSQL.
