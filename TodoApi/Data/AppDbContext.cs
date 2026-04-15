using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

namespace TodoApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
            entity.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
            entity.Property(x => x.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.Property(x => x.Title).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Details).HasMaxLength(1000);
            entity.Property(x => x.Priority).HasConversion<string>();
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasOne(x => x.User)
                .WithMany(x => x.Todos)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
