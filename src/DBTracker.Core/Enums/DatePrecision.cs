namespace DBTracker.Core.Enums;

/// <summary>How precisely a vendor-published date (D02) is actually known — vendor pages often publish a quarter or year, not a day. Unknown/TBD dates must stay explicit rather than being defaulted to a fabricated day.</summary>
public enum DatePrecision
{
    Exact,
    Month,
    Year,
    Unknown
}
