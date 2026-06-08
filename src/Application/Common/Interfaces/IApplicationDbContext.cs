using Invensys.ITrace.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Invensys.ITrace.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<ApplicationRegistration> Applications { get; }

    DbSet<TelemetryRecord> TelemetryRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
