namespace MngOperations.Application.Rules;

public enum RulePhase
{
    PreValidation,
    Default,
    PostValidation,
    Automation
}

public static class RuleTypes
{
    public const string Validation = "validation";
    public const string Default = "default";
    public const string Automation = "automation";
}

public static class RuleTriggers
{
    public const string WorkItemCreated = "WorkItemCreated";
    public const string WorkItemUpdated = "WorkItemUpdated";
    public const string WorkItemTransition = "WorkItemTransition";
    public const string WorkItemTransitioned = "WorkItemTransitioned";
}
