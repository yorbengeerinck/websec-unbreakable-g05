using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Unbreakable.Data;
using Unbreakable.Models;

namespace Unbreakable.Auth;

// Houdt de tabel Users bij op basis van het token (sub + preferred_username). De cache voorkomt dat
// elke request de databank raakt; ze wordt enkel gevuld nadat de rij correct staat.
public sealed class UserDirectory
{
    private readonly ConcurrentDictionary<string, string> _known = new();

    // Leegt de cache (gebruikt door de tests nadat de tabel Users geleegd is).
    public void Clear() => _known.Clear();

    public async Task EnsureAsync(UnbreakableContext db, string sub, string username, CancellationToken cancellationToken)
    {
        if (_known.TryGetValue(sub, out var cached) && cached == username)
        {
            return;
        }

        var user = await db.Users.FindAsync(new object[] { sub }, cancellationToken);
        if (user == null)
        {
            db.Users.Add(new User { Sub = sub, Username = username, CreatedAt = DateTime.UtcNow });
        }
        else if (user.Username != username)
        {
            user.Username = username;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Gelijktijdige eerste request met dezelfde sub: dan staat de rij er al en is dit geen fout.
            db.ChangeTracker.Clear();
            if (!await db.Users.AnyAsync(u => u.Sub == sub && u.Username == username, cancellationToken))
            {
                throw;
            }
        }

        // Lab 3-rijen zonder sub-kolommen krijgen hun sub. Dit maakt ze NIET leesbaar: zonder
        // tuples in OpenFGA blijven ze onzichtbaar (fail closed).
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Messages\" SET \"SenderSub\" = {sub} WHERE \"SenderSub\" IS NULL AND \"Sender\" = {username}",
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Messages\" SET \"RecipientSub\" = {sub} WHERE \"RecipientSub\" IS NULL AND \"Recipient\" = {username}",
            cancellationToken);

        _known[sub] = username;
    }
}
