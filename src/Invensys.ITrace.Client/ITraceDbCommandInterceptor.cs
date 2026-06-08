using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace Invensys.ITrace.Client;

public sealed class ITraceDbCommandInterceptor(
    IITraceTelemetryClient telemetryClient,
    IOptionsMonitor<ITraceOptions> options) : DbCommandInterceptor
{
    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Track(command, eventData, "Reader");
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Track(command, eventData, "Reader");
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        Track(command, eventData, "NonQuery");
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Track(command, eventData, "NonQuery");
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        Track(command, eventData, "Scalar");
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Track(command, eventData, "Scalar");
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        Track(command, eventData, "CommandFailed");
        _ = telemetryClient.TrackErrorAsync(eventData.Exception, "Database command");
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Track(command, eventData, "CommandFailed");
        return telemetryClient.TrackErrorAsync(
            eventData.Exception,
            "Database command",
            cancellationToken: cancellationToken).AsTask();
    }

    private void Track(DbCommand command, CommandExecutedEventData eventData, string operation)
    {
        if (!options.CurrentValue.CaptureDbDurations)
        {
            return;
        }

        _ = telemetryClient.TrackDbDurationAsync(
            operation,
            eventData.Duration.TotalMilliseconds,
            command.Connection?.Database,
            eventData.Context?.Database.ProviderName,
            command.CommandText,
            BuildAttributes(command, eventData));
    }

    private void Track(DbCommand command, CommandErrorEventData eventData, string operation)
    {
        if (!options.CurrentValue.CaptureDbDurations)
        {
            return;
        }

        _ = telemetryClient.TrackDbDurationAsync(
            operation,
            eventData.Duration.TotalMilliseconds,
            command.Connection?.Database,
            eventData.Context?.Database.ProviderName,
            command.CommandText,
            BuildAttributes(command, eventData));
    }

    private static Dictionary<string, string?> BuildAttributes(DbCommand command, CommandEndEventData eventData) => new()
    {
        ["db.command_timeout"] = command.CommandTimeout.ToString(),
        ["db.command_type"] = command.CommandType.ToString(),
        ["ef.command_source"] = eventData.CommandSource.ToString(),
    };
}
