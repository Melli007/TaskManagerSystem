using EtechTaskManagerBackend.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;

namespace EtechTaskManagerBackend.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {  }
            public DbSet<Users> Users { get; set; }
            public DbSet<Notifications> Notifications { get; set; }
            public DbSet<Tasks> Tasks { get; set; }
            public DbSet<Messages> Messages { get; set; } // Add Messages DbSet

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // This is important to preserve Identity table configurations

            // Configure the foreign key relationship between Tasks and Users
            modelBuilder.Entity<Tasks>()
                .HasOne(t => t.User)
                .WithMany(u => u.Tasks)
                .HasForeignKey(t => t.AssignedTo)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Users>()
                .HasMany(u => u.CreatedTasks)
                .WithOne(t => t.CreatedByUser)
                .HasForeignKey(t => t.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);


            // Configure the foreign key relationship between Notifications and Users
            modelBuilder.Entity<Notifications>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.Recipient)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure default value for Banned column in Users table
            modelBuilder.Entity<Users>()
                .Property(u => u.Banned)
                .HasDefaultValue(false); // Default value is false


            // Configure the Messages entity
            modelBuilder.Entity<Messages>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading deletes for Sender

            modelBuilder.Entity<Messages>()
            .HasOne(m => m.Recipient)
            .WithMany()
            .HasForeignKey(m => m.RecipientId)
            .OnDelete(DeleteBehavior.Cascade); // Enable cascade delete
             // Prevent cascading deletes for Recipient

            // Configure default value for IsRead in Messages
            modelBuilder.Entity<Messages>()
                .Property(m => m.IsRead)
                .HasDefaultValue(false); // Default to false
        }
    }
}