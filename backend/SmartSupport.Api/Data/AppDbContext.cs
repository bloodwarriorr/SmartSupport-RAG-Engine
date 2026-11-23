using Microsoft.EntityFrameworkCore;
using SmartSupport.Api.Models;

namespace SmartSupport.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<KnowledgeDocument> Documents { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Membership> Memberships { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Organization>(org =>
            {
                org.Property(o => o.Name).HasMaxLength(100).IsRequired();
                org.Property(o => o.InviteCode).HasMaxLength(16).IsRequired();
                org.HasIndex(o => o.InviteCode).IsUnique();
            });

            modelBuilder.Entity<Membership>(m =>
            {
                m.HasKey(x => new { x.UserId, x.OrganizationId });
                // One organization per user for now. Drop this unique index to allow several.
                m.HasIndex(x => x.UserId).IsUnique();
                m.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
                m.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
                m.HasOne(x => x.Organization).WithMany(o => o.Memberships).HasForeignKey(x => x.OrganizationId);
            });

            modelBuilder.Entity<KnowledgeDocument>()
                .HasOne(d => d.Organization)
                .WithMany()
                .HasForeignKey(d => d.OrganizationId);

            modelBuilder.Entity<ChatSession>()
                .HasOne<Organization>()
                .WithMany()
                .HasForeignKey(s => s.OrganizationId);

            // One to many relation
            modelBuilder.Entity<DocumentChunk>()
                .HasOne(c => c.KnowledgeDocument)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.KnowledgeDocumentId);
        }
    }
}
