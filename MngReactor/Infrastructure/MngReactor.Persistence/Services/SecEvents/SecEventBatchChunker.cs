namespace MngReactor.Persistence.Services.SecEvents;

internal static class SecEventBatchChunker
{
    public static IEnumerable<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T> items, int chunkSize)
    {
        if (items.Count == 0)
            yield break;

        if (chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize));

        for (var offset = 0; offset < items.Count; offset += chunkSize)
        {
            var count = Math.Min(chunkSize, items.Count - offset);
            var chunk = new T[count];
            for (var i = 0; i < count; i++)
                chunk[i] = items[offset + i];

            yield return chunk;
        }
    }
}
