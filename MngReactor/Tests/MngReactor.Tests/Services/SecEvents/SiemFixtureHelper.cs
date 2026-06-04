namespace MngReactor.Tests.Services.SecEvents;

internal static class SiemFixtureHelper
{
    public static string ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "siem", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"SIEM fixture not found: {path}", path);

        return File.ReadAllText(path).TrimEnd();
    }
}
