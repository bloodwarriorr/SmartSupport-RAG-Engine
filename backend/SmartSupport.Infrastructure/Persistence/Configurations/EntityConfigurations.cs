using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartSupport.Domain.Entities;

namespace SmartSupport.Infrastructure.Persistence.Configurations;

// Database mappings, kept out of the Domain entities. Table and column names match the
// existing migrations exactly, so this refactor needs no database change.

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(u => u.Id);
        b.Property(u => u.GoogleId).IsRequired();
        b.Property(u => u.Email).IsRequired();

        b.HasMany(u => u.ChatSessions)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> b)
    {
        b.ToTable("ChatSessions");
        b.HasKey(s => s.Id);
        b.Property(s => s.Title).IsRequired();

        b.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(s => s.OrganizationId);

        b.HasMany(s => s.Messages)
            .WithOne()
            .HasForeignKey(m => m.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.ToTable("ChatMessages");
        b.HasKey(m => m.Id);
        b.Property(m => m.Content).IsRequired();
        b.Property(m => m.Role).IsRequired();
        b.Property(m => m.UserEmail).IsRequired();
    }
}

internal sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> b)
    {
        b.ToTable("Documents");
        b.HasKey(d => d.Id);
        b.Property(d => d.FileName).IsRequired();
        b.Property(d => d.ContentType).IsRequired();

        b.HasOne(d => d.Organization)
            .WithMany()
            .HasForeignKey(d => d.OrganizationId);

        b.HasMany(d => d.Chunks)
            .WithOne(c => c.KnowledgeDocument)
            .HasForeignKey(c => c.KnowledgeDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> b)
    {
        b.ToTable("DocumentChunks");
        b.HasKey(c => c.Id);
        b.Property(c => c.TextContent).IsRequired();
        b.Property(c => c.VectorId).IsRequired();
    }
}

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("Organizations");
        b.HasKey(o => o.Id);
        b.Property(o => o.Name).HasMaxLength(100).IsRequired();
        b.Property(o => o.InviteCode).HasMaxLength(16).IsRequired();
        b.HasIndex(o => o.InviteCode).IsUnique();
    }
}

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> b)
    {
        b.ToTable("Memberships");
        b.HasKey(m => new { m.UserId, m.OrganizationId });
        // One organization per user for now. Drop this unique index to allow several.
        b.HasIndex(m => m.UserId).IsUnique();
        b.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        b.Ignore(m => m.IsAdmin);

        b.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(m => m.Organization)
            .WithMany(o => o.Memberships)
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
