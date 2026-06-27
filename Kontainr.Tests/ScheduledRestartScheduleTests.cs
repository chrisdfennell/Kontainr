using Kontainr.Services;

namespace Kontainr.Tests;

public class ScheduledRestartScheduleTests
{
    // ── Daily "HH:mm" ────────────────────────────────────────────
    [Fact]
    public void Daily_matches_on_exact_hour_and_minute()
    {
        var now = new DateTime(2026, 6, 27, 3, 30, 0);
        Assert.True(ScheduledRestartService.ShouldRun("03:30", now));
    }

    [Fact]
    public void Daily_does_not_match_other_minute()
    {
        var now = new DateTime(2026, 6, 27, 3, 31, 0);
        Assert.False(ScheduledRestartService.ShouldRun("03:30", now));
    }

    [Fact]
    public void Daily_ignores_seconds()
    {
        var now = new DateTime(2026, 6, 27, 3, 30, 59);
        Assert.True(ScheduledRestartService.ShouldRun("03:30", now));
    }

    // ── Weekly "ddd HH:mm" ───────────────────────────────────────
    [Fact]
    public void Weekly_matches_on_listed_day()
    {
        // 2026-06-27 is a Saturday
        var sat = new DateTime(2026, 6, 27, 6, 0, 0);
        Assert.True(ScheduledRestartService.ShouldRun("Sat 06:00", sat));
    }

    [Fact]
    public void Weekly_does_not_match_other_day()
    {
        var sat = new DateTime(2026, 6, 27, 6, 0, 0);
        Assert.False(ScheduledRestartService.ShouldRun("Mon 06:00", sat));
    }

    [Fact]
    public void Weekly_supports_multiple_days_csv()
    {
        var sat = new DateTime(2026, 6, 27, 6, 0, 0);
        Assert.True(ScheduledRestartService.ShouldRun("Mon,Sat 06:00", sat));
    }

    [Fact]
    public void Weekly_day_match_is_case_insensitive()
    {
        var sat = new DateTime(2026, 6, 27, 6, 0, 0);
        Assert.True(ScheduledRestartService.ShouldRun("sat 06:00", sat));
    }

    // ── Interval "*/N" (every N hours, on the hour) ──────────────
    [Fact]
    public void Interval_matches_when_hour_is_multiple_and_minute_zero()
    {
        var now = new DateTime(2026, 6, 27, 6, 0, 0); // 6 % 6 == 0
        Assert.True(ScheduledRestartService.ShouldRun("*/6", now));
    }

    [Fact]
    public void Interval_does_not_match_off_the_hour()
    {
        var now = new DateTime(2026, 6, 27, 6, 5, 0);
        Assert.False(ScheduledRestartService.ShouldRun("*/6", now));
    }

    [Fact]
    public void Interval_does_not_match_non_multiple_hour()
    {
        var now = new DateTime(2026, 6, 27, 7, 0, 0); // 7 % 6 != 0
        Assert.False(ScheduledRestartService.ShouldRun("*/6", now));
    }

    // ── Malformed expressions never throw, never run ─────────────
    [Theory]
    [InlineData("not-a-time")]
    [InlineData("25:99")]
    [InlineData("")]
    [InlineData("*/abc")]
    public void Malformed_expression_returns_false(string expr)
    {
        var now = new DateTime(2026, 6, 27, 6, 0, 0);
        Assert.False(ScheduledRestartService.ShouldRun(expr, now));
    }

    [Fact]
    public void Whitespace_around_expression_is_tolerated()
    {
        var now = new DateTime(2026, 6, 27, 3, 30, 0);
        Assert.True(ScheduledRestartService.ShouldRun("  03:30  ", now));
    }
}
