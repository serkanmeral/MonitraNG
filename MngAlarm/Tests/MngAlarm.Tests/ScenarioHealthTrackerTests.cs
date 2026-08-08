using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Evaluation;

namespace MngAlarm.Tests.Evaluation;

public sealed class ScenarioHealthTrackerTests
{
    [Fact]
    public void Success_resets_errors_to_healthy()
    {
        var health = new ScenarioRuntimeHealth
        {
            Level = ScenarioHealthLevels.Error,
            ConsecutiveErrors = 5
        };

        ScenarioHealthTracker.RecordSuccess(health, DateTime.UtcNow);

        Assert.Equal(ScenarioHealthLevels.Healthy, health.Level);
        Assert.Equal(0, health.ConsecutiveErrors);
        Assert.NotNull(health.LastSuccessAt);
    }

    [Fact]
    public void Errors_escalate_from_warning_to_error()
    {
        var health = new ScenarioRuntimeHealth();
        var now = DateTime.UtcNow;

        ScenarioHealthTracker.RecordError(health, now, "Boom", "first");
        Assert.Equal(ScenarioHealthLevels.Warning, health.Level);

        ScenarioHealthTracker.RecordError(health, now.AddSeconds(1), "Boom", "second");
        Assert.Equal(ScenarioHealthLevels.Warning, health.Level);

        ScenarioHealthTracker.RecordError(health, now.AddSeconds(2), "Boom", "third");
        Assert.Equal(ScenarioHealthLevels.Error, health.Level);
        Assert.Equal(3, health.ConsecutiveErrors);
        Assert.Equal("third", health.LastErrorMessage);
    }

    [Fact]
    public void Operational_status_uses_published_enabled_flag()
    {
        var draft = new ScenarioVersionDocument { Status = ScenarioLifecycleStatuses.Draft, Enabled = false };
        var publishedOn = new ScenarioVersionDocument { Status = ScenarioLifecycleStatuses.Published, Enabled = true };
        var publishedOff = new ScenarioVersionDocument { Status = ScenarioLifecycleStatuses.Published, Enabled = false };
        var archived = new ScenarioVersionDocument { Status = ScenarioLifecycleStatuses.Archived };

        Assert.Equal(ScenarioOperationalStatuses.Draft, ScenarioHealthTracker.ResolveOperationalStatus(null, draft));
        Assert.Equal(ScenarioOperationalStatuses.Running, ScenarioHealthTracker.ResolveOperationalStatus(publishedOn, draft));
        Assert.Equal(ScenarioOperationalStatuses.Stopped, ScenarioHealthTracker.ResolveOperationalStatus(publishedOff, draft));
        Assert.Equal(ScenarioOperationalStatuses.Archived, ScenarioHealthTracker.ResolveOperationalStatus(null, archived));
    }
}
