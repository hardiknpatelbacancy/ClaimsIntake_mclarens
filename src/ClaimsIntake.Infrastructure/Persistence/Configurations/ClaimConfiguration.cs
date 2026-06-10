using ClaimsIntake.Domain.Entities;
using ClaimsIntake.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsIntake.Infrastructure.Persistence.Configurations;

public sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    /// <summary>
    /// Maps the <see cref="Claim"/> aggregate: column constraints, the status-as-string conversion,
    /// and indexes on the columns the list endpoint filters by.
    /// </summary>
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.PolicyNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.ClaimantName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(c => c.IncidentDate)
            .IsRequired();

        builder.Property(c => c.SubmittedAt)
            .IsRequired();

        builder.Property(c => c.LastUpdatedAt)
            .IsRequired();

        // Stored as a string: readable in ad-hoc queries and safe if enum members are reordered.
        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(c => c.ReviewNotes)
            .HasMaxLength(4000);

        builder.HasIndex(c => c.PolicyNumber);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.SubmittedAt);
    }
}
