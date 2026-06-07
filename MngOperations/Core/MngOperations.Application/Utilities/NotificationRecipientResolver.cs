using MngOperations.Application.Utilities;

namespace MngOperations.Application.Utilities;

public static class NotificationRecipientResolver
{
    public static IReadOnlyList<string> Resolve(
        IReadOnlyDictionary<string, object?> workItem,
        IReadOnlyList<string> recipientRoles,
        string? actor,
        bool excludeActor)
    {
        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in recipientRoles)
        {
            if (string.IsNullOrWhiteSpace(role))
                continue;

            var normalized = role.Trim();
            switch (normalized.ToLowerInvariant())
            {
                case "assignee":
                    AddIfPresent(recipients, WorkItemDataHelper.GetPersonRefId(workItem, "assignee"));
                    break;
                case "reporter":
                    AddIfPresent(recipients, WorkItemDataHelper.GetPersonRefId(workItem, "reporter"));
                    break;
                case "watchers":
                    foreach (var watcher in WorkItemDataHelper.GetPersonRefIdList(workItem, "watchers"))
                        AddIfPresent(recipients, watcher);
                    break;
                case "actor":
                    AddIfPresent(recipients, actor);
                    break;
                default:
                    if (normalized.StartsWith("field:", StringComparison.OrdinalIgnoreCase))
                    {
                        var fieldKey = normalized[6..].Trim();
                        if (!string.IsNullOrWhiteSpace(fieldKey))
                        {
                            AddIfPresent(recipients, WorkItemDataHelper.GetPersonRefId(workItem, fieldKey));
                            foreach (var personId in WorkItemDataHelper.GetPersonRefIdList(workItem, fieldKey))
                                AddIfPresent(recipients, personId);
                        }
                    }
                    else
                    {
                        AddIfPresent(recipients, normalized);
                    }

                    break;
            }
        }

        if (excludeActor && !string.IsNullOrWhiteSpace(actor))
            recipients.Remove(actor);

        return recipients.ToList();
    }

    public static IReadOnlyList<string> ToEmailAddresses(
        IEnumerable<string> recipients,
        string? emailDomainSuffix)
    {
        var emails = new List<string>();

        foreach (var recipient in recipients)
        {
            if (string.IsNullOrWhiteSpace(recipient))
                continue;

            if (recipient.Contains('@', StringComparison.Ordinal))
            {
                emails.Add(recipient.Trim());
                continue;
            }

            if (!string.IsNullOrWhiteSpace(emailDomainSuffix))
            {
                var suffix = emailDomainSuffix.StartsWith('@') ? emailDomainSuffix : "@" + emailDomainSuffix;
                emails.Add($"{recipient.Trim()}{suffix}");
            }
        }

        return emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddIfPresent(HashSet<string> recipients, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            recipients.Add(value.Trim());
    }
}
