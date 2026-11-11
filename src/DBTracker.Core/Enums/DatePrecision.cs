namespace DBTracker.Core.Enums;

/// <summary>How precisely a vendor date is known. Vendors often publish only a quarter or year, so unknown and TBD dates stay explicit instead of becoming an invented day.</summary>
public enum DatePrecision
{
    Exact,
    Month,
    Year,
    Unknown
}
