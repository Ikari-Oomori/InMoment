using InMoment.Domain.Contacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InMoment.Infrastructure.Persistence.Configurations;

public sealed class ContactImportLogConfiguration : IEntityTypeConfiguration<ContactImportLog>
{
    public void Configure(EntityTypeBuilder<ContactImportLog> b)
    {
        b.ToTable("contact_import_logs");

        b.HasKey(x => x.Id);

        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.ContactsSubmitted).IsRequired();
        b.Property(x => x.MatchesFound).IsRequired();

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.CreatedAtUtc);
    }
}