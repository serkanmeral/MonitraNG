namespace MngOperations.Application.WorkspacePacks;

internal static class WorkspacePackValidator
{
    private static readonly HashSet<string> WorkspaceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "team", "service_desk", "operational", "project"
    };

    private static readonly HashSet<string> StateCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "open", "in_progress", "closed"
    };

    public static IReadOnlyList<string> Validate(WorkspacePackDefinition pack)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(pack.Code)) errors.Add("Paket kodu zorunludur.");
        if (string.IsNullOrWhiteSpace(pack.Name)) errors.Add("Paket adı zorunludur.");
        if (!string.IsNullOrWhiteSpace(pack.SuggestedWorkspaceType)
            && !WorkspaceTypes.Contains(pack.SuggestedWorkspaceType.Trim()))
            errors.Add("Önerilen workspace tipi geçersiz.");

        var stateKeys = Keys(pack.States.Select(s => s.Key), "Durum", errors);
        var stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var state in pack.States)
        {
            if (string.IsNullOrWhiteSpace(state.Name))
                errors.Add($"Durum adı zorunludur ({state.Key}).");
            else if (!stateNames.Add(state.Name.Trim()))
                errors.Add($"Durum adı yineleniyor: {state.Name}.");
            if (!StateCategories.Contains((state.Category ?? string.Empty).Trim()))
                errors.Add($"Durum kategorisi geçersiz: {state.Key}.");
        }

        var initial = pack.States.Where(s => s.IsInitial).Select(s => s.Key).ToList();
        if (initial.Count != 1)
            errors.Add("Pakette tek başlangıç durumu olmalıdır.");

        var flowKeys = Keys(pack.Flows.Select(f => f.Key), "Akış", errors);
        if (pack.Flows.Count(f => f.IsDefault) != 1)
            errors.Add("Tek varsayılan akış olmalıdır.");

        var transitionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var flow in pack.Flows)
        {
            if (!stateKeys.Contains(flow.InitialStateKey))
                errors.Add($"Akışın başlangıç durumu yok: {flow.Key}.");
            else if (initial.Count == 1 && flow.IsDefault && !string.Equals(flow.InitialStateKey, initial[0], StringComparison.OrdinalIgnoreCase))
                errors.Add("Varsayılan akışın başlangıcı, başlangıç durumu olmalıdır.");
            if (flow.Transitions.Count == 0)
                errors.Add($"Akışta geçiş yok: {flow.Key}.");
            foreach (var transition in flow.Transitions)
            {
                if (string.IsNullOrWhiteSpace(transition.Key) || !transitionKeys.Add(transition.Key))
                    errors.Add($"Geçiş anahtarı eksik veya yineleniyor: {transition.Key}.");
                if (!stateKeys.Contains(transition.FromStateKey) || !stateKeys.Contains(transition.ToStateKey))
                    errors.Add($"Geçiş tanımsız duruma bakıyor: {transition.Key}.");
                if (string.IsNullOrWhiteSpace(transition.Label))
                    errors.Add($"Geçiş etiketi zorunludur: {transition.Key}.");
            }
        }

        var typeKeys = Keys(pack.Types.Select(t => t.Key), "İş tipi", errors);
        if (typeKeys.Count == 0)
            errors.Add("En az bir iş tipi olmalıdır.");
        foreach (var type in pack.Types)
        {
            if (!flowKeys.Contains(type.FlowKey))
                errors.Add($"İş tipi tanımsız akışa bakıyor: {type.Key}.");
            foreach (var child in type.ChildTypeKeys)
            {
                if (!typeKeys.Contains(child))
                    errors.Add($"Alt tip tanımsız: {type.Key} -> {child}.");
                if (string.Equals(child, type.Key, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"İş tipi kendisinin altına giremez: {type.Key}.");
            }
        }

        RequireOneDefault(pack.Forms.Count(f => f.IsDefault), "form", errors);
        RequireOneDefault(pack.Boards.Count(b => b.IsDefault), "pano", errors);
        RequireOneDefault(pack.Profiles.Count(p => p.IsDefault), "profil", errors);
        Keys(pack.Forms.Select(f => f.Key), "Form", errors);
        Keys(pack.Boards.Select(b => b.Key), "Pano", errors);
        Keys(pack.Profiles.Select(p => p.Key), "Profil", errors);

        foreach (var form in pack.Forms)
            RequireFlowAndType(form.Key, form.FlowKey, form.TypeKey, flowKeys, typeKeys, errors);
        foreach (var board in pack.Boards)
        {
            RequireFlowAndType(board.Key, board.FlowKey, board.TypeKey, flowKeys, typeKeys, errors);
            if (!pack.Forms.Any(f => string.Equals(f.Key, board.FormKey, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"Pano tanımsız forma bakıyor: {board.Key}.");
            foreach (var column in board.ColumnStateKeys)
            {
                if (!stateKeys.Contains(column))
                    errors.Add($"Pano sütunu tanımsız duruma bakıyor: {board.Key}.");
            }
        }
        foreach (var profile in pack.Profiles)
        {
            RequireFlowAndType(profile.Key, profile.FlowKey, profile.TypeKey, flowKeys, typeKeys, errors);
            foreach (var action in profile.ActionKeys)
            {
                if (!transitionKeys.Contains(action))
                    errors.Add($"Profil tanımsız geçişe bakıyor: {profile.Key} -> {action}.");
            }
        }

        foreach (var folder in pack.LibraryFolders)
        {
            var name = (folder ?? string.Empty).Trim();
            if (name.Length == 0 || name.Contains('/') || name.Contains('\\'))
                errors.Add("Kütüphane klasörü kökün altına göreli bir ad olmalıdır.");
        }

        return errors;
    }

    private static void RequireOneDefault(int count, string label, List<string> errors)
    {
        if (count != 1)
            errors.Add($"Tek varsayılan {label} olmalıdır.");
    }

    private static void RequireFlowAndType(
        string ownerKey,
        string flowKey,
        string typeKey,
        HashSet<string> flowKeys,
        HashSet<string> typeKeys,
        List<string> errors)
    {
        if (!flowKeys.Contains(flowKey))
            errors.Add($"Parça tanımsız akışa bakıyor: {ownerKey}.");
        if (!typeKeys.Contains(typeKey))
            errors.Add($"Parça tanımsız iş tipine bakıyor: {ownerKey}.");
    }

    private static HashSet<string> Keys(IEnumerable<string> keys, string label, List<string> errors)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            var value = (key ?? string.Empty).Trim();
            if (value.Length == 0 || !set.Add(value))
                errors.Add($"{label} anahtarı eksik veya yineleniyor.");
        }
        return set;
    }
}
