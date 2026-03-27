using CdrBilling.Domain.Enums;

namespace CdrBilling.Domain.Services;

public readonly record struct TarificationCall(
    long Id,
    DateTimeOffset StartTime,
    CallDirection Direction,
    Disposition Disposition,
    int BillableSec,
    string DigitsToRate);
