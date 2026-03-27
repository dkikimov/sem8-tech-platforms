using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using CdrBilling.Application.UseCases;
using CdrBilling.Domain.Entities;
using CdrBilling.Domain.Enums;

namespace CdrBilling.Infrastructure.Parsing;

/// <summary>
/// Streams CDR records from a pipe-delimited text file using System.IO.Pipelines.
/// Zero-copy buffer management — suitable for very large files.
/// Format: StartTime|EndTime|CallingParty|CalledParty|CallDirection|Disposition|Duration|BillableSec|Charge|AccountCode|CallID|TrunkName
/// </summary>
public sealed class CdrFileParser : ICdrFileParser
{
    public async IAsyncEnumerable<CallRecord> ParseAsync(
        Stream stream,
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var pipeReader = PipeReader.Create(stream, new StreamPipeReaderOptions(bufferSize: 65536));
        try
        {
            while (true)
            {
                var result = await pipeReader.ReadAsync(ct);
                var buffer = result.Buffer;

                while (TryReadLine(ref buffer, result.IsCompleted, out var line))
                {
                    var record = TryParseLine(line, sessionId);
                    if (record is not null)
                        yield return record;
                }

                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted) break;
            }
        }
        finally
        {
            await pipeReader.CompleteAsync();
        }
    }

    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, bool isCompleted, out ReadOnlySequence<byte> line)
    {
        var reader = new SequenceReader<byte>(buffer);
        if (reader.TryReadTo(out line, (byte)'\n'))
        {
            buffer = buffer.Slice(reader.Position);
            return true;
        }
        // Handle file with no trailing newline: only return remaining bytes as the last line
        // when the pipe is fully completed; otherwise wait for more data to arrive.
        if (isCompleted && !buffer.IsEmpty)
        {
            line = buffer;
            buffer = buffer.Slice(buffer.End);
            return true;
        }
        line = default;
        return false;
    }

    private static CallRecord? TryParseLine(ReadOnlySequence<byte> lineSeq, Guid sessionId)
    {
        try
        {
            // Decode to string — acceptable cost vs. Span-based parsing for correctness
            var text = Encoding.UTF8.GetString(lineSeq).TrimEnd('\r', '\n').Trim();
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (IsHeader(text)) return null;

            var textSpan = text.AsSpan();
            Span<Range> ranges = stackalloc Range[12];
            if (!TrySplitIntoRanges(textSpan, '|', ranges)) return null;

            var startTime = DateTimeOffset.Parse(textSpan[ranges[0]].Trim());
            var endTime = DateTimeOffset.Parse(textSpan[ranges[1]].Trim());
            var calling = textSpan[ranges[2]].Trim().ToString();
            var called = textSpan[ranges[3]].Trim().ToString();
            var direction = ParseDirection(textSpan[ranges[4]].Trim());
            var disposition = ParseDisposition(textSpan[ranges[5]].Trim());
            var duration = int.Parse(textSpan[ranges[6]].Trim());
            var billable = int.Parse(textSpan[ranges[7]].Trim());

            var chargeSpan = textSpan[ranges[8]].Trim();
            decimal? charge = chargeSpan.IsEmpty
                ? null
                : decimal.Parse(chargeSpan, System.Globalization.CultureInfo.InvariantCulture);

            var accountSpan = textSpan[ranges[9]].Trim();
            var account = accountSpan.IsEmpty ? null : accountSpan.ToString();
            var callId = textSpan[ranges[10]].Trim().ToString();
            var trunkSpan = textSpan[ranges[11]].Trim();
            var trunk = trunkSpan.IsEmpty ? null : trunkSpan.ToString();

            return CallRecord.Create(sessionId, startTime, endTime, calling, called,
                direction, disposition, duration, billable, charge, account, callId, trunk);
        }
        catch
        {
            // Skip malformed lines
            return null;
        }
    }

    private static bool TrySplitIntoRanges(ReadOnlySpan<char> text, char separator, Span<Range> ranges)
    {
        var start = 0;
        var count = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != separator)
                continue;

            if (count >= ranges.Length)
                return false;

            ranges[count++] = start..i;
            start = i + 1;
        }

        if (count != ranges.Length - 1)
            return false;

        ranges[count] = start..text.Length;
        return true;
    }

    private static CallDirection ParseDirection(ReadOnlySpan<char> value)
        => value.Equals("outgoing".AsSpan(), StringComparison.OrdinalIgnoreCase) ? CallDirection.Outgoing
         : value.Equals("internal".AsSpan(), StringComparison.OrdinalIgnoreCase) ? CallDirection.Internal
         : CallDirection.Incoming;

    private static Disposition ParseDisposition(ReadOnlySpan<char> value)
        => value.Equals("answered".AsSpan(), StringComparison.OrdinalIgnoreCase) ? Disposition.Answered
         : value.Equals("busy".AsSpan(), StringComparison.OrdinalIgnoreCase) ? Disposition.Busy
         : value.Equals("no_answer".AsSpan(), StringComparison.OrdinalIgnoreCase) ? Disposition.NoAnswer
         : Disposition.Failed;

    private static bool IsHeader(string line)
        => line.StartsWith("StartTime|", StringComparison.OrdinalIgnoreCase);
}
