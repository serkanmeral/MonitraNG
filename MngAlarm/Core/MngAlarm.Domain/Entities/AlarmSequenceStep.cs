namespace MngAlarm.Domain.Entities;

/// <summary>Ordered step in a sequence alarm rule (e.g. N× login_failed → login_success).</summary>
public sealed class AlarmSequenceStep
{
    public string MatchKey { get; set; } = string.Empty;

    /// <summary>Minimum matching events in <see cref="WithinMinutes"/> (step 0).</summary>
    public int MinCount { get; set; } = 1;

    /// <summary>Sliding window for step 0 accumulation (minutes).</summary>
    public int WithinMinutes { get; set; }

    /// <summary>Deadline after anchor (first step-0 event) for this step (minutes).</summary>
    public int WithinMinutesAfterFirst { get; set; }
}
