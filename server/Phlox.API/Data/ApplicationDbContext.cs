using Microsoft.EntityFrameworkCore;
using Phlox.API.Entities;

namespace Phlox.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.KeycloakId)
                .IsUnique();

            entity.HasIndex(e => e.Email);

            entity.Property(e => e.KeycloakId)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Email)
                .HasMaxLength(255);

            entity.Property(e => e.Name)
                .HasMaxLength(255);

            entity.Property(e => e.PreferredUsername)
                .HasMaxLength(255);
        });
    }
}
