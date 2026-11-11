using Microsoft.EntityFrameworkCore;
using Unbreakable.Models;

namespace Unbreakable.Data;

public class UnbreakableContext : DbContext
{
    public UnbreakableContext(DbContextOptions<UnbreakableContext> options)
        : base(options)
    {
    }

    public DbSet<Message> Messages { get; set; }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Namen komen overeen met SchemaUpgrade.Sql, zodat een nieuwe en een bestaande databank gelijk zijn.
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique().HasDatabaseName("IX_Users_Username");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasIndex(m => m.SenderSub).HasDatabaseName("IX_Messages_SenderSub");
            entity.HasIndex(m => m.RecipientSub).HasDatabaseName("IX_Messages_RecipientSub");
        });
    }
}
