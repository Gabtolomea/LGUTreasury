namespace LGUTreasury;

/// <summary>
/// Minimal, dependency-free .env file loader.
/// It reads KEY=VALUE lines (ignoring blanks and # comments) and exposes them as
/// environment variables so ASP.NET Core configuration (e.g. ConnectionStrings__X)
/// can pick them up without committing secrets to appsettings.json.
/// </summary>
public static class DotEnv
{
    public static void Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            // Strip optional surrounding quotes.
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // Do not override variables already provided by the host/OS.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>
    /// Loads a .env file from the current working directory and/or the app base directory.
    /// </summary>
    public static void LoadDefault()
    {
        Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
        Load(Path.Combine(AppContext.BaseDirectory, ".env"));
    }
}
