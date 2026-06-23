using Invensys.ITrace.Application.Common.Interfaces;
using Invensys.ITrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Invensys.ITrace.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<ApplicationRegistration> Applications => Set<ApplicationRegistration>();

    public DbSet<TelemetryRecord> TelemetryRecords => Set<TelemetryRecord>();

    public DbSet<PerformanceCounterSample> PerformanceCounterSamples => Set<PerformanceCounterSample>();

    public DbSet<PerformanceCounterDailySummary> PerformanceCounterDailySummaries => Set<PerformanceCounterDailySummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationRegistration>(entity =>
        {
            entity.ToTable("Applications");
            entity.HasKey(application => application.Id);
            entity.HasIndex(application => application.DsnHash).IsUnique();
            entity.HasIndex(application => new { application.Name, application.Environment, application.SiteName }).IsUnique();
            entity.Property(application => application.Name).HasMaxLength(160);
            entity.Property(application => application.Environment).HasMaxLength(80);
            entity.Property(application => application.SiteName).HasMaxLength(160);
            entity.Property(application => application.Description).HasMaxLength(1000);
            entity.Property(application => application.Dsn).HasMaxLength(512);
            entity.Property(application => application.DsnHash).HasMaxLength(128);
        });

        modelBuilder.Entity<TelemetryRecord>(entity =>
        {
            entity.ToTable("TelemetryRecords");
            entity.HasKey(record => record.Id);
            entity.HasIndex(record => new { record.ApplicationId, record.Signal, record.OccurredAtUtc });
            entity.HasIndex(record => record.TraceId);
            entity.Property(record => record.Signal).HasConversion<string>().HasMaxLength(40);
            entity.Property(record => record.Severity).HasMaxLength(40);
            entity.Property(record => record.Message).HasMaxLength(4000);
            entity.Property(record => record.Operation).HasMaxLength(512);
            entity.Property(record => record.Route).HasMaxLength(512);
            entity.Property(record => record.Method).HasMaxLength(20);
            entity.Property(record => record.Database).HasMaxLength(256);
            entity.Property(record => record.DbSystem).HasMaxLength(80);
            entity.Property(record => record.DbStatement).HasMaxLength(4096);
            entity.Property(record => record.ExceptionType).HasMaxLength(512);
            entity.Property(record => record.TraceId).HasMaxLength(64);
            entity.Property(record => record.SpanId).HasMaxLength(32);
            entity.HasOne(record => record.Application)
                .WithMany(application => application.TelemetryRecords)
                .HasForeignKey(record => record.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PerformanceCounterSample>(entity =>
        {
            entity.ToTable("PerformanceCounterSamples");
            entity.HasKey(sample => sample.Id);
            entity.HasIndex(sample => new { sample.Scope, sample.SourceId, sample.OccurredAtUtc });
            entity.Property(sample => sample.Scope).HasConversion<string>().HasMaxLength(40);
            entity.Property(sample => sample.SourceId).HasMaxLength(128);
            entity.Property(sample => sample.SourceName).HasMaxLength(256);
        });

        modelBuilder.Entity<PerformanceCounterDailySummary>(entity =>
        {
            entity.ToTable("PerformanceCounterDailySummaries");
            entity.HasKey(summary => summary.Id);
            entity.HasIndex(summary => new { summary.Day, summary.Scope, summary.SourceId, summary.Metric }).IsUnique();
            entity.Property(summary => summary.Scope).HasConversion<string>().HasMaxLength(40);
            entity.Property(summary => summary.Metric).HasConversion<string>().HasMaxLength(80);
            entity.Property(summary => summary.SourceId).HasMaxLength(128);
            entity.Property(summary => summary.SourceName).HasMaxLength(256);
        });
    }
}
