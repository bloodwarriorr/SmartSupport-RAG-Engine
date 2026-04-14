using Microsoft.EntityFrameworkCore;
using SmartSupport.Api.Models;

namespace SmartSupport.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<KnowledgeDocument> Documents { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // One to many relation
            modelBuilder.Entity<DocumentChunk>()
                .HasOne(c => c.KnowledgeDocument)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.KnowledgeDocumentId);
        }
    }
}
