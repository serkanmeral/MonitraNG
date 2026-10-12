using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services;

/// <summary>Taban belgenin indeks metnini seçilen belgelerle paragraf düzeyinde karşılaştırır.</summary>
public sealed class DocumentDiffService : IDocumentDiffService
{
    private const int MaxOthers = 4;
    private const int MaxParagraphs = 20000;
    private const int MaxShown = 60;
    private const double ChangeSimilarity = 0.35;

    private readonly IResourceService _resources;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRequestContext _ctx;
    private readonly MngDocumentSettings _settings;

    public DocumentDiffService(
        IResourceService resources,
        IHttpClientFactory httpClientFactory,
        IRequestContext ctx,
        IOptions<MngDocumentSettings> settings)
    {
        _resources = resources;
        _httpClientFactory = httpClientFactory;
        _ctx = ctx;
        _settings = settings.Value;
    }

    public async Task<ResourceDiffResultDto> CompareAsync(
        string baseId,
        IReadOnlyList<string> otherIds,
        CancellationToken ct = default)
    {
        var basis = await _resources.GetByIdAsync(baseId, ct);
        if (!string.Equals(basis.IndexStatus, "done", StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "DIFF_NEEDS_INDEX",
                "The base document must be indexed.",
                "Fark için taban belgenin indeksi tamamlanmış olmalı.");
        }

        var domain = string.IsNullOrWhiteSpace(_ctx.DomainName) ? "default" : _ctx.DomainName.Trim();
        var baseText = await ReadIndexedTextAsync(domain, baseId, ct);
        if (string.IsNullOrWhiteSpace(baseText))
        {
            throw DocumentException.Validation(
                "DIFF_TEXT_MISSING",
                "Indexed text was not found.",
                "Taban belgenin indeks metni bulunamadı.");
        }

        var baseParagraphs = Paragraphs(baseText);
        var ids = (otherIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxOthers)
            .ToList();

        var comparisons = new List<ResourceDiffComparisonDto>();
        foreach (var id in ids)
        {
            if (string.Equals(id, baseId, StringComparison.Ordinal))
            {
                comparisons.Add(Excluded(id, basis.Name, "Belge kendisiyle karşılaştırılamaz."));
                continue;
            }

            try
            {
                var other = await _resources.GetByIdAsync(id, ct);
                if (!string.Equals(other.IndexStatus, "done", StringComparison.OrdinalIgnoreCase))
                {
                    comparisons.Add(Excluded(id, other.Name, "Belgenin indeksi tamamlanmamış."));
                    continue;
                }

                var text = await ReadIndexedTextAsync(domain, id, ct);
                if (string.IsNullOrWhiteSpace(text))
                {
                    comparisons.Add(Excluded(id, other.Name, "İndeks metni bulunamadı."));
                    continue;
                }

                comparisons.Add(CompareOne(other.Id, other.Name, baseParagraphs, Paragraphs(text)));
            }
            catch (DocumentException ex)
            {
                comparisons.Add(Excluded(id, id, ex.MessageTr ?? "Belge okunamadı."));
            }
        }

        return new ResourceDiffResultDto
        {
            BaseId = basis.Id,
            BaseName = basis.Name,
            Comparisons = comparisons
        };
    }

    private static ResourceDiffComparisonDto CompareOne(
        string id,
        string name,
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        var truncated = left.Count > MaxParagraphs || right.Count > MaxParagraphs;
        var a = left.Take(MaxParagraphs).ToList();
        var b = right.Take(MaxParagraphs).ToList();
        var ops = Align(a, b);
        var added = new List<TaggedLine>();
        var removed = new List<TaggedLine>();
        var changed = new List<ResourceDiffChangeDto>();
        var unchanged = 0;
        var heading = string.Empty;
        var index = 0;
        while (index < ops.Count)
        {
            if (ops[index].Kind == '=')
            {
                unchanged++;
                if (IsHeading(ops[index].Text))
                    heading = ops[index].Text;
                index++;
                continue;
            }

            var deletes = new List<TaggedLine>();
            var inserts = new List<TaggedLine>();
            while (index < ops.Count && ops[index].Kind != '=')
            {
                var line = new TaggedLine(ops[index].Text, heading);
                if (ops[index].Kind == '-')
                    deletes.Add(line);
                else
                    inserts.Add(line);
                if (IsHeading(ops[index].Text))
                    heading = ops[index].Text;
                index++;
            }

            var canPair = deletes.Count > 0 && inserts.Count > 0 && deletes.Count <= 40 && inserts.Count <= 40;
            if (!canPair)
            {
                removed.AddRange(deletes);
                added.AddRange(inserts);
                continue;
            }

            var used = new bool[inserts.Count];
            foreach (var deleted in deletes)
            {
                var best = -1;
                var bestScore = ChangeSimilarity;
                for (var i = 0; i < inserts.Count; i++)
                {
                    if (used[i])
                        continue;
                    var score = Similarity(deleted.Text, inserts[i].Text);
                    if (score < bestScore)
                        continue;
                    bestScore = score;
                    best = i;
                }

                if (best >= 0)
                {
                    used[best] = true;
                    changed.Add(new ResourceDiffChangeDto
                    {
                        Base = Clip(deleted.Text),
                        Other = Clip(inserts[best].Text),
                        Heading = deleted.Heading
                    });
                }
                else
                {
                    removed.Add(deleted);
                }
            }

            for (var i = 0; i < inserts.Count; i++)
            {
                if (!used[i])
                    added.Add(inserts[i]);
            }
        }

        var signalAdded = added.Where(line => !IsNoise(line.Text)).ToList();
        var noiseAdded = added.Where(line => IsNoise(line.Text)).ToList();
        var signalRemoved = removed.Where(line => !IsNoise(line.Text)).ToList();
        var noiseRemoved = removed.Where(line => IsNoise(line.Text)).ToList();
        var signalChanged = changed.Where(change => !IsNoiseChange(change)).ToList();
        var noiseChanged = changed.Where(IsNoiseChange).ToList();
        var shownAdded = signalAdded.Take(MaxShown).Select(ToLine).ToList();
        var shownRemoved = signalRemoved.Take(MaxShown).Select(ToLine).ToList();
        var shownChanged = signalChanged.Take(MaxShown).ToList();
        var shownNoiseAdded = noiseAdded.Take(MaxShown).Select(ToLine).ToList();
        var shownNoiseRemoved = noiseRemoved.Take(MaxShown).Select(ToLine).ToList();
        var shownNoiseChanged = noiseChanged.Take(MaxShown).ToList();
        var listsCapped = signalAdded.Count > MaxShown || signalRemoved.Count > MaxShown || signalChanged.Count > MaxShown
            || noiseAdded.Count > MaxShown || noiseRemoved.Count > MaxShown || noiseChanged.Count > MaxShown;
        return new ResourceDiffComparisonDto
        {
            Id = id,
            Name = name,
            Compared = true,
            UnchangedCount = unchanged,
            Truncated = truncated || listsCapped,
            AddedCount = signalAdded.Count,
            RemovedCount = signalRemoved.Count,
            ChangedCount = signalChanged.Count,
            Added = shownAdded,
            Removed = shownRemoved,
            Changed = shownChanged,
            NoiseAddedCount = noiseAdded.Count,
            NoiseRemovedCount = noiseRemoved.Count,
            NoiseChangedCount = noiseChanged.Count,
            NoiseAdded = shownNoiseAdded,
            NoiseRemoved = shownNoiseRemoved,
            NoiseChanged = shownNoiseChanged
        };
    }

    private static List<DiffOp> Align(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var leftKey = left.Select(Normalize).ToArray();
        var rightKey = right.Select(Normalize).ToArray();
        var ops = new List<DiffOp>();
        var x = 0;
        var y = 0;
        foreach (var (anchorLeft, anchorRight) in UniqueAnchors(leftKey, rightKey))
        {
            if (anchorLeft < x || anchorRight < y)
                continue;
            AppendRange(ops, left, right, leftKey, rightKey, x, anchorLeft, y, anchorRight);
            ops.Add(new DiffOp('=', left[anchorLeft]));
            x = anchorLeft + 1;
            y = anchorRight + 1;
        }

        AppendRange(ops, left, right, leftKey, rightKey, x, left.Count, y, right.Count);
        return ops;
    }

    private static void AppendRange(
        List<DiffOp> ops,
        IReadOnlyList<string> left,
        IReadOnlyList<string> right,
        string[] leftKey,
        string[] rightKey,
        int iStart,
        int iEnd,
        int jStart,
        int jEnd)
    {
        var i = iStart;
        var j = jStart;
        while (i < iEnd && j < jEnd && leftKey[i] == rightKey[j])
        {
            ops.Add(new DiffOp('=', left[i]));
            i++;
            j++;
        }

        var iTail = iEnd;
        var jTail = jEnd;
        while (iTail > i && jTail > j && leftKey[iTail - 1] == rightKey[jTail - 1])
        {
            iTail--;
            jTail--;
        }

        var rows = iTail - i;
        var cols = jTail - j;
        if (rows == 0)
        {
            for (var k = j; k < jTail; k++)
                ops.Add(new DiffOp('+', right[k]));
        }
        else if (cols == 0)
        {
            for (var k = i; k < iTail; k++)
                ops.Add(new DiffOp('-', left[k]));
        }
        else if ((long)rows * cols <= 250_000)
        {
            AppendLcs(ops, left, right, leftKey, rightKey, i, iTail, j, jTail);
        }
        else
        {
            AppendGreedy(ops, left, right, leftKey, rightKey, i, iTail, j, jTail);
        }

        for (var k = 0; k < iEnd - iTail; k++)
            ops.Add(new DiffOp('=', left[iTail + k]));
    }

    private static void AppendLcs(
        List<DiffOp> ops,
        IReadOnlyList<string> left,
        IReadOnlyList<string> right,
        string[] leftKey,
        string[] rightKey,
        int iStart,
        int iEnd,
        int jStart,
        int jEnd)
    {
        var rows = iEnd - iStart;
        var cols = jEnd - jStart;
        var dp = new int[rows + 1, cols + 1];
        for (var i = rows - 1; i >= 0; i--)
        {
            for (var j = cols - 1; j >= 0; j--)
            {
                dp[i, j] = leftKey[iStart + i] == rightKey[jStart + j]
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var x = 0;
        var y = 0;
        while (x < rows && y < cols)
        {
            if (leftKey[iStart + x] == rightKey[jStart + y])
            {
                ops.Add(new DiffOp('=', left[iStart + x]));
                x++;
                y++;
            }
            else if (dp[x + 1, y] >= dp[x, y + 1])
            {
                ops.Add(new DiffOp('-', left[iStart + x]));
                x++;
            }
            else
            {
                ops.Add(new DiffOp('+', right[jStart + y]));
                y++;
            }
        }

        while (x < rows)
            ops.Add(new DiffOp('-', left[iStart + x++]));
        while (y < cols)
            ops.Add(new DiffOp('+', right[jStart + y++]));
    }

    private static void AppendGreedy(
        List<DiffOp> ops,
        IReadOnlyList<string> left,
        IReadOnlyList<string> right,
        string[] leftKey,
        string[] rightKey,
        int iStart,
        int iEnd,
        int jStart,
        int jEnd)
    {
        var positions = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
        for (var j = jStart; j < jEnd; j++)
        {
            if (!positions.TryGetValue(rightKey[j], out var queue))
            {
                queue = new Queue<int>();
                positions[rightKey[j]] = queue;
            }

            queue.Enqueue(j);
        }

        var cursor = jStart;
        var pending = new List<int>();
        for (var i = iStart; i < iEnd; i++)
        {
            if (!positions.TryGetValue(leftKey[i], out var queue))
            {
                pending.Add(i);
                continue;
            }

            while (queue.Count > 0 && queue.Peek() < cursor)
                queue.Dequeue();
            if (queue.Count == 0)
            {
                pending.Add(i);
                continue;
            }

            var match = queue.Dequeue();
            foreach (var deleted in pending)
                ops.Add(new DiffOp('-', left[deleted]));
            pending.Clear();
            for (var j = cursor; j < match; j++)
                ops.Add(new DiffOp('+', right[j]));
            ops.Add(new DiffOp('=', left[i]));
            cursor = match + 1;
        }

        foreach (var deleted in pending)
            ops.Add(new DiffOp('-', left[deleted]));
        for (var j = cursor; j < jEnd; j++)
            ops.Add(new DiffOp('+', right[j]));
    }

    private static List<(int Left, int Right)> UniqueAnchors(string[] left, string[] right)
    {
        var leftCount = new Dictionary<string, int>(StringComparer.Ordinal);
        var rightCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in left)
            leftCount[line] = leftCount.GetValueOrDefault(line) + 1;
        foreach (var line in right)
            rightCount[line] = rightCount.GetValueOrDefault(line) + 1;

        var rightIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var j = 0; j < right.Length; j++)
        {
            if (rightCount[right[j]] == 1)
                rightIndex[right[j]] = j;
        }

        var pairs = new List<(int Left, int Right)>();
        for (var i = 0; i < left.Length; i++)
        {
            if (leftCount[left[i]] != 1)
                continue;
            if (rightIndex.TryGetValue(left[i], out var j))
                pairs.Add((i, j));
        }

        return LongestIncreasing(pairs);
    }

    private static List<(int Left, int Right)> LongestIncreasing(List<(int Left, int Right)> pairs)
    {
        var count = pairs.Count;
        if (count == 0)
            return pairs;

        var tails = new List<int>();
        var previous = new int[count];
        Array.Fill(previous, -1);
        for (var i = 0; i < count; i++)
        {
            var lo = 0;
            var hi = tails.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (pairs[tails[mid]].Right < pairs[i].Right)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            if (lo > 0)
                previous[i] = tails[lo - 1];
            if (lo == tails.Count)
                tails.Add(i);
            else
                tails[lo] = i;
        }

        var chain = new List<(int Left, int Right)>();
        for (var i = tails[^1]; i >= 0; i = previous[i])
            chain.Add(pairs[i]);
        chain.Reverse();
        return chain;
    }

    private async Task<string> ReadIndexedTextAsync(string domain, string id, CancellationToken ct)
    {
        var baseUrl = (_settings.OpenSearch.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return string.Empty;

        var client = _httpClientFactory.CreateClient("OpenSearch");
        using var response = await client.GetAsync($"{baseUrl}/{DiSearchIndex.Name(domain)}/_source/{id}", ct);
        if (!response.IsSuccessStatusCode)
            return string.Empty;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty;
    }

    private static ResourceDiffComparisonDto Excluded(string id, string name, string reason) =>
        new()
        {
            Id = id,
            Name = name,
            Compared = false,
            Reason = reason
        };

    private static List<string> Paragraphs(string text) =>
        text.Replace("\r\n", "\n").Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

    private static string Normalize(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ");

    private static readonly Regex HeadingNumber = new(@"^\d+(?:\.\d+)*[.)]?\s+\p{L}", RegexOptions.Compiled);
    private static readonly Regex TocLeader = new(@"\.{3,}", RegexOptions.Compiled);

    private static bool IsHeading(string value)
    {
        var text = value.Trim();
        if (text.Length is < 3 or > 120)
            return false;
        if (text.Contains('\t') || TocLeader.IsMatch(text))
            return false;
        if (text.EndsWith('.') || text.EndsWith(';') || text.EndsWith(',') || text.EndsWith(':'))
            return false;
        if (text.StartsWith('#'))
            return true;
        if (HeadingNumber.IsMatch(text))
            return true;

        var letters = 0;
        var upper = 0;
        foreach (var ch in text)
        {
            if (!char.IsLetter(ch))
                continue;
            letters++;
            if (char.IsUpper(ch))
                upper++;
        }

        return letters >= 3 && text.Length <= 80 && upper >= letters * 0.7;
    }

    private static readonly Regex NumberOnly = new(@"^[\d\s.\-/:]+$", RegexOptions.Compiled);

    private static bool IsNoise(string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
            return true;
        if (TocLeader.IsMatch(text))
            return true;
        return NumberOnly.IsMatch(text);
    }

    private static bool IsNoiseChange(ResourceDiffChangeDto change) =>
        IsNoise(change.Base) && IsNoise(change.Other);

    private static ResourceDiffLineDto ToLine(TaggedLine line) =>
        new()
        {
            Text = Clip(line.Text),
            Heading = line.Heading
        };

    private static double Similarity(string left, string right)
    {
        var a = Words(left);
        var b = Words(right);
        if (a.Count == 0 || b.Count == 0)
            return 0;
        var shared = a.Intersect(b).Count();
        var union = a.Count + b.Count - shared;
        return union == 0 ? 0 : (double)shared / union;
    }

    private static HashSet<string> Words(string value) =>
        Regex.Split(Normalize(value).ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(word => word.Length > 1)
            .ToHashSet(StringComparer.Ordinal);

    private static string Clip(string value) =>
        value.Length <= 700 ? value : value[..700] + "…";

    private readonly record struct DiffOp(char Kind, string Text);
    private readonly record struct TaggedLine(string Text, string Heading);
}
