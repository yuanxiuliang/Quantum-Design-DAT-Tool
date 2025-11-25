namespace DatTool.Tests;

internal static class TestDataHelper
{
    public static string GetDataPath(string filename)
    {
        var baseDir = AppContext.BaseDirectory;
        var testDataDir = Path.Combine(baseDir, "TestData");
        return Path.Combine(testDataDir, filename);
    }
}

