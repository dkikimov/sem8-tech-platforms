using CdrBilling.Application.UseCases;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CdrBilling.Api.Endpoints;

public static class UploadEndpoints
{
    private const long MaxUploadSizeBytes = 2L * 1024 * 1024 * 1024;

    public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions/{sessionId:guid}").WithTags("Upload");

        // POST /api/sessions/{sessionId}/upload/cdr
        group.MapPost("/upload/cdr", async (
            Guid sessionId,
            IFormFile file,
            ISender sender,
            CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadCdrCommand(stream, sessionId), ct);
            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxUploadSizeBytes })
        .WithMetadata(new RequestSizeLimitAttribute(MaxUploadSizeBytes))
        .WithName("UploadCdr")
        .WithSummary("Upload CDR file (pipe-delimited)");

        // POST /api/sessions/{sessionId}/upload/tariff
        group.MapPost("/upload/tariff", async (
            Guid sessionId,
            IFormFile file,
            ISender sender,
            CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadTariffCommand(stream, sessionId), ct);
            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxUploadSizeBytes })
        .WithMetadata(new RequestSizeLimitAttribute(MaxUploadSizeBytes))
        .WithName("UploadTariff")
        .WithSummary("Upload tariff file (semicolon CSV)");

        // POST /api/sessions/{sessionId}/upload/subscribers
        group.MapPost("/upload/subscribers", async (
            Guid sessionId,
            IFormFile file,
            ISender sender,
            CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await sender.Send(new UploadSubscriberCommand(stream, sessionId), ct);
            return Results.Ok(result);
        })
        .DisableAntiforgery()
        .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = MaxUploadSizeBytes })
        .WithMetadata(new RequestSizeLimitAttribute(MaxUploadSizeBytes))
        .WithName("UploadSubscribers")
        .WithSummary("Upload subscriber base file (semicolon CSV)");

        return app;
    }
}
