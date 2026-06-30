using DomainApplication = Invensys.ITrace.Domain.Entities.Application;
using Invensys.ITrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Invensys.ITrace.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<DomainApplication> Applications { get; }

    DbSet<TelemetryRecord> TelemetryRecords { get; }

    DbSet<PerformanceCounterSample> PerformanceCounterSamples { get; }

    DbSet<PerformanceCounterDailySummary> PerformanceCounterDailySummaries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
