using CdrBilling.Application.Abstractions;
using CdrBilling.Application.Options;
using CdrBilling.Domain.Enums;
using CdrBilling.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CdrBilling.Application.UseCases;

public sealed record RunTarificationCommand(Guid SessionId) : IRequest;

public sealed class RunTarificationHandler(
    ICallRecordRepository cdrRepo,
    ITariffRepository tariffRepo,
    IBillingSessionRepository sessionRepo,
    ISessionProgressReporter progress,
    TarificationOptions tarificationOptions,
    ILogger<RunTarificationHandler> logger)
    : IRequestHandler<RunTarificationCommand>
{
    public async Task Handle(RunTarificationCommand request, CancellationToken cancellationToken)
    {
        var sessionId = request.SessionId;
        var batchSize = Math.Clamp(tarificationOptions.BatchSize, 100, 20_000);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Count total CDR records for progress tracking
            var total = await cdrRepo.CountAsync(sessionId, cancellationToken);

            var session = await sessionRepo.GetAsync(sessionId, cancellationToken)
                ?? throw new InvalidOperationException($"Session {sessionId} not found.");

            session.SetRunning(total);
            await sessionRepo.UpdateAsync(session, cancellationToken);

            // Load all tariffs into memory and build the prefix trie engine
            var tariffs = await tariffRepo.GetAllForSessionAsync(sessionId, cancellationToken);
            var engine = new TarificationEngine(tariffs);

            logger.LogInformation(
                "Tariffication started for session {SessionId}: {Total} records, {TariffCount} tariffs, batch size {BatchSize}.",
                sessionId, total, tariffs.Count, batchSize);

            var processed = 0;
            var matched = 0;
            var processedSinceFlush = 0;
            var updates = new List<(long Id, decimal Charge, long TariffId)>(batchSize);
            await using var batchUpdater = await cdrRepo.CreateChargeBatchUpdaterAsync(cancellationToken);

            await foreach (var call in cdrRepo.GetUnratedAsync(sessionId, cancellationToken))
            {
                var match = engine.FindBestTariff(call);

                if (match is not null)
                {
                    updates.Add((call.Id, match.Charge, match.Tariff.Id));
                    matched++;
                }
                // Records with no tariff match keep ComputedCharge = null (unrated)

                processed++;
                processedSinceFlush++;

                if (processedSinceFlush >= batchSize)
                {
                    if (updates.Count > 0)
                    {
                        await batchUpdater.ApplyAsync(updates, cancellationToken);
                        updates.Clear();
                    }

                    processedSinceFlush = 0;
                    await progress.ReportAsync(sessionId, processed, total, cancellationToken);

                    logger.LogDebug(
                        "Tariffication progress: {Processed}/{Total}, matched {Matched}",
                        processed,
                        total,
                        matched);
                }
            }

            // Final flush
            if (updates.Count > 0)
            {
                await batchUpdater.ApplyAsync(updates, cancellationToken);
                updates.Clear();
            }

            await progress.ReportAsync(sessionId, processed, total, cancellationToken);

            session = await sessionRepo.GetAsync(sessionId, cancellationToken)
                ?? throw new InvalidOperationException($"Session {sessionId} not found after tariffication.");
            session.MarkCompleted();
            await sessionRepo.UpdateAsync(session, cancellationToken);

            await progress.ReportCompletedAsync(sessionId, processed, total, cancellationToken);

            logger.LogInformation(
                "Tariffication completed for session {SessionId}: {Processed} records processed, {Matched} matched in {ElapsedMs} ms.",
                sessionId, processed, matched, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Tariffication failed for session {SessionId}.", sessionId);

            try
            {
                var session = await sessionRepo.GetAsync(sessionId, CancellationToken.None);
                if (session is not null)
                {
                    session.MarkFailed(ex.Message);
                    await sessionRepo.UpdateAsync(session, CancellationToken.None);
                }
                await progress.ReportFailedAsync(sessionId, ex.Message, CancellationToken.None);
            }
            catch (Exception inner)
            {
                logger.LogError(inner, "Failed to persist error state for session {SessionId}.", sessionId);
            }
        }
    }
}
