using DBTracker.Tray.Core;

namespace DBTracker.Tray.Tests;

public class SummaryTests
{
    private const string SummaryJson = """
        {"generatedAtUtc":"2026-11-11T08:00:00Z","user":"priya.nair@architonixlabs.com","failed":2,"overdue":1,"neverBackedUp":0,
         "atRisk":[{"id":7,"name":"SALES-SQL","health":"Failed","environment":"Production","lastSuccessUtc":"2026-11-09T22:00:00Z"},
                   {"id":9,"name":"HR-MYSQL","health":"Overdue","environment":"Production","lastSuccessUtc":null}],
         "dashboardUrl":"/"}
        """;

    private static TraySummary Summary(params long[] ids) => new()
    {
        AtRisk = ids.Select(i => new AtRiskInstance { Id = i, Name = "I" + i, Health = "Failed" }).ToArray(),
    };

    [Fact]
    public void ParsesSummary()
    {
        var summary = TrayJson.ParseSummary(SummaryJson)!;
        Assert.Equal(2, summary.Failed);
        Assert.Equal(1, summary.Overdue);
        Assert.Equal(2, summary.AtRisk.Length);
        Assert.Equal("SALES-SQL", summary.AtRisk[0].Name);
        Assert.Equal("Backups: 2 failed, 1 overdue, 0 never backed up", TrayText.Backups(summary));
    }

    [Fact]
    public void MissingAtRiskList_BecomesEmpty()
    {
        var summary = TrayJson.ParseSummary("""{"failed":0,"overdue":0,"neverBackedUp":0}""")!;
        Assert.Empty(summary.AtRisk);
    }

    [Fact]
    public void MalformedSummary_IsNull()
    {
        Assert.Null(TrayJson.ParseSummary("<html>login</html>"));
    }

    [Fact]
    public void AtRiskTracker_FirstPollIsBaseline()
    {
        var tracker = new AtRiskTracker();
        Assert.Empty(tracker.Update(Summary(1, 2)));
    }

    [Fact]
    public void AtRiskTracker_ReportsOnlyNewIds()
    {
        var tracker = new AtRiskTracker();
        tracker.Update(Summary(1, 2));
        var added = tracker.Update(Summary(2, 3));
        Assert.Equal(new long[] { 3 }, added.Select(a => a.Id));
    }

    [Fact]
    public void AtRiskTracker_RecoveredThenFailingAgain_NotifiesAgain()
    {
        var tracker = new AtRiskTracker();
        tracker.Update(Summary(1));
        tracker.Update(Summary());
        Assert.Single(tracker.Update(Summary(1)));
    }

    [Fact]
    public void NewlyAtRiskText_SingleAndMany()
    {
        var one = new[] { new AtRiskInstance { Id = 1, Name = "SALES-SQL", Health = "Failed", Environment = "Production" } };
        Assert.Equal("SALES-SQL is now Failed (Production)", TrayText.NewlyAtRisk(one));
        var many = new[] { one[0], new AtRiskInstance { Id = 2, Name = "HR" } };
        Assert.StartsWith("2 instances are newly at risk", TrayText.NewlyAtRisk(many));
        Assert.Null(TrayText.NewlyAtRisk(Array.Empty<AtRiskInstance>()));
    }
}
