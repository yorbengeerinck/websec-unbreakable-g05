using Microsoft.EntityFrameworkCore;

namespace Unbreakable.Data;

// Idempotente schema-uitbreidingen. EnsureCreated maakt een nieuwe databank volledig aan, maar voegt
// niets toe aan een bestaande; daarom doen we de uitbreidingen met IF NOT EXISTS (zoals in opdracht 3).
public static class SchemaUpgrade
{
    public const string Sql =
        // Opdracht 3
        "ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"KeyLine\" integer NULL; " +
        "ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"Mac\" character varying(64) NULL; " +
        // Opdracht 4 (Y6): Keycloak-gebruikers en sub-kolommen.
        "CREATE TABLE IF NOT EXISTS \"Users\" (" +
        "\"Sub\" character varying(100) NOT NULL PRIMARY KEY, " +
        "\"Username\" character varying(40) NOT NULL, " +
        "\"CreatedAt\" timestamp with time zone NOT NULL); " +
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Users_Username\" ON \"Users\" (\"Username\"); " +
        "ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"SenderSub\" character varying(100) NULL; " +
        "ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"RecipientSub\" character varying(100) NULL; " +
        "CREATE INDEX IF NOT EXISTS \"IX_Messages_SenderSub\" ON \"Messages\" (\"SenderSub\"); " +
        "CREATE INDEX IF NOT EXISTS \"IX_Messages_RecipientSub\" ON \"Messages\" (\"RecipientSub\");";

    public static void Apply(UnbreakableContext context)
    {
        context.Database.ExecuteSqlRaw(Sql);
    }
}
