using System;
using System.IO;

namespace Fantasy;

public static class ServerGameConfigPath
{
    public static string FindGameCoreConfigDirectory()
    {
        string? environmentPath = Environment.GetEnvironmentVariable("DG_GAMECORE_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(environmentPath) && Directory.Exists(environmentPath))
        {
            return environmentPath;
        }

        if (TryFindFrom(AppContext.BaseDirectory, out string found))
        {
            return found;
        }

        if (TryFindFrom(Directory.GetCurrentDirectory(), out found))
        {
            return found;
        }

        throw new DirectoryNotFoundException("Cannot find Config/Luban/Generated/json.");
    }

    private static bool TryFindFrom(string startPath, out string dataDirectory)
    {
        DirectoryInfo? directory = new DirectoryInfo(startPath);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "Config", "Luban", "Generated", "json");
            if (Directory.Exists(candidate))
            {
                dataDirectory = candidate;
                return true;
            }

            directory = directory.Parent;
        }

        dataDirectory = string.Empty;
        return false;
    }
}
