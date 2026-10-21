using JobApplicationAgent.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : DbContext(options)
{
    public DbSet<ResumeDocument> Resumes => Set<ResumeDocument>();
    public DbSet<ResumeTextExtraction> Extractions => Set<ResumeTextExtraction>();
    public DbSet<ResumeCandidateFact> CandidateFacts => Set<ResumeCandidateFact>();
    public DbSet<ResumeSeries> ResumeSeries => Set<ResumeSeries>();
    public DbSet<ResumeSeriesVersion> ResumeVersions => Set<ResumeSeriesVersion>();
    public DbSet<PreparedDocumentDraft> PreparedDrafts => Set<PreparedDocumentDraft>();
    public DbSet<DocumentComposition> Compositions => Set<DocumentComposition>();
    public DbSet<DocumentExport> Exports => Set<DocumentExport>();
    public DbSet<DocumentApplicationBinding> ApplicationBindings => Set<DocumentApplicationBinding>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        var binding = builder.Entity<DocumentApplicationBinding>();
        binding.ToTable("document_application_bindings");
        binding.HasKey(x => x.ExportId);
        binding.Property(x => x.Kind).HasMaxLength(30);
        binding.Property(x => x.FileSha256).HasMaxLength(64);
        binding.Property(x => x.CompositionSha256).HasMaxLength(64);
        binding.Property(x => x.SnapshotSha256).HasMaxLength(64);
        binding.HasOne<DocumentExport>().WithMany().HasForeignKey(x => new { x.ExportId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        var document = builder.Entity<ResumeDocument>();
        document.ToTable("resume_documents");
        document.HasKey(x => x.Id);
        document.HasAlternateKey(x => new { x.Id, x.OwnerId });
        document.Property(x => x.FileName).HasMaxLength(200).IsRequired();
        document.Property(x => x.MediaType).HasMaxLength(100).IsRequired();
        document.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        document.Property(x => x.Status).HasMaxLength(50).IsRequired();
        document.Property(x => x.Content).HasColumnType("bytea").IsRequired();
        document.HasIndex(x => new { x.OwnerId, x.UploadedAtUtc, x.Id });
        var extraction = builder.Entity<ResumeTextExtraction>();
        extraction.ToTable("resume_text_extractions");
        extraction.HasKey(x => x.Id);
        extraction.Property(x => x.SourceSha256).HasMaxLength(64).IsRequired();
        extraction.Property(x => x.EngineVersion).HasMaxLength(100).IsRequired();
        extraction.Property(x => x.ReviewStatus).HasMaxLength(50).IsRequired();
        extraction.Property(x => x.ResultJson).HasColumnType("jsonb").IsRequired();
        extraction.HasIndex(x => new { x.ResumeId, x.EngineVersion }).IsUnique();
        extraction.HasIndex(x => new { x.OwnerId, x.ResumeId });
        extraction.HasOne<ResumeDocument>().WithMany().HasForeignKey(x => x.ResumeId).OnDelete(DeleteBehavior.Cascade);
        var fact = builder.Entity<ResumeCandidateFact>();
        fact.ToTable("resume_candidate_facts");
        fact.HasKey(x => x.Id);
        fact.Property(x => x.SourceSha256).HasMaxLength(64).IsRequired();
        fact.Property(x => x.EngineVersion).HasMaxLength(100).IsRequired();
        fact.Property(x => x.Kind).HasMaxLength(50).IsRequired();
        fact.Property(x => x.UnitKind).HasMaxLength(30).IsRequired();
        fact.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        fact.Property(x => x.FieldsJson).HasColumnType("jsonb").IsRequired();
        fact.Property(x => x.Decision).HasMaxLength(30).IsRequired();
        fact.Property(x => x.ReviewNote).HasMaxLength(1000);
        fact.Property(x => x.ProfileDatesJson).HasColumnType("jsonb");
        fact.HasIndex(x => new { x.ExtractionId, x.Fingerprint }).IsUnique();
        fact.HasIndex(x => new { x.OwnerId, x.ResumeId, x.Decision, x.CreatedAtUtc, x.Id });
        fact.HasOne<ResumeTextExtraction>().WithMany().HasForeignKey(x => x.ExtractionId).OnDelete(DeleteBehavior.Cascade);
        var series = builder.Entity<ResumeSeries>();
        series.ToTable("resume_series");
        series.HasKey(x => x.Id);
        series.HasAlternateKey(x => new { x.Id, x.OwnerId });
        series.Property(x => x.Name).HasMaxLength(200).IsRequired();
        series.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc, x.Id });
        var version = builder.Entity<ResumeSeriesVersion>();
        version.ToTable("resume_series_versions", table => table.HasCheckConstraint("CK_resume_series_version_number", "\"VersionNumber\" BETWEEN 1 AND 1000"));
        version.HasKey(x => x.ResumeId);
        version.Property(x => x.Label).HasMaxLength(200);
        version.HasIndex(x => new { x.SeriesId, x.VersionNumber }).IsUnique();
        version.HasOne<ResumeSeries>().WithMany().HasForeignKey(x => new { x.SeriesId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Cascade);
        version.HasOne<ResumeDocument>().WithMany().HasForeignKey(x => new { x.ResumeId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        var draft = builder.Entity<PreparedDocumentDraft>();
        draft.ToTable("prepared_document_drafts", table => table.HasCheckConstraint("CK_prepared_draft_snapshot_size", "octet_length(\"SnapshotJson\") BETWEEN 1 AND 2097152"));
        draft.HasKey(x => x.Id);
        draft.HasAlternateKey(x => new { x.Id, x.OwnerId });
        draft.Property(x => x.Kind).HasMaxLength(30).IsRequired();
        draft.Property(x => x.Status).HasMaxLength(40).IsRequired();
        draft.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        draft.Property(x => x.SnapshotSha256).HasMaxLength(64).IsRequired();
        // Preserve the exact serialized bytes used by SnapshotSha256, without JSONB rewriting.
        draft.Property(x => x.SnapshotJson).HasColumnType("text").IsRequired();
        draft.HasIndex(x => new { x.OwnerId, x.RequestId }).IsUnique();
        draft.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc, x.Id });
        draft.HasOne<ResumeDocument>().WithMany().HasForeignKey(x => new { x.ResumeId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        var composition = builder.Entity<DocumentComposition>();
        composition.ToTable("document_compositions", table => table.HasCheckConstraint("CK_composition_content_size", "octet_length(\"ContentJson\") BETWEEN 1 AND 2097152"));
        composition.HasKey(x => x.DraftId);
        composition.HasAlternateKey(x => new { x.DraftId, x.OwnerId });
        composition.Property(x => x.Kind).HasMaxLength(30).IsRequired();
        composition.Property(x => x.TemplateVersion).HasMaxLength(100).IsRequired();
        composition.Property(x => x.SourceSnapshotSha256).HasMaxLength(64).IsRequired();
        composition.Property(x => x.ContentSha256).HasMaxLength(64).IsRequired();
        composition.Property(x => x.ContentJson).HasColumnType("text").IsRequired();
        composition.Property(x => x.Decision).HasMaxLength(30).IsRequired();
        composition.Property(x => x.ReviewNote).HasMaxLength(1000);
        composition.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc, x.DraftId });
        composition.HasOne<PreparedDocumentDraft>().WithMany().HasForeignKey(x => new { x.DraftId, x.OwnerId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
        var export = builder.Entity<DocumentExport>();
        export.ToTable("document_exports", table => table.HasCheckConstraint("CK_document_export_length", "octet_length(\"Content\") BETWEEN 1 AND 5242880 AND \"Length\" = octet_length(\"Content\")"));
        export.HasKey(x => x.Id);
        export.Property(x => x.Format).HasMaxLength(10).IsRequired();
        export.Property(x => x.RendererVersion).HasMaxLength(100).IsRequired();
        export.Property(x => x.CompositionSha256).HasMaxLength(64).IsRequired();
        export.Property(x => x.FileSha256).HasMaxLength(64).IsRequired();
        export.Property(x => x.FileName).HasMaxLength(100).IsRequired();
        export.Property(x => x.MediaType).HasMaxLength(100).IsRequired();
        export.Property(x => x.Content).HasColumnType("bytea").IsRequired();
        export.HasIndex(x => new { x.DraftId, x.Format }).IsUnique();
        export.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc, x.Id });
        export.HasOne<DocumentComposition>().WithMany().HasForeignKey(x => new { x.DraftId, x.OwnerId })
            .HasPrincipalKey(x => new { x.DraftId, x.OwnerId }).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class DocumentsDbContextFactory : IDesignTimeDbContextFactory<DocumentsDbContext>
{
    public DocumentsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<DocumentsDbContext>().UseNpgsql(
        Environment.GetEnvironmentVariable("ConnectionStrings__DocumentsDatabase") ?? throw new InvalidOperationException("Set ConnectionStrings__DocumentsDatabase.")).Options);
}
