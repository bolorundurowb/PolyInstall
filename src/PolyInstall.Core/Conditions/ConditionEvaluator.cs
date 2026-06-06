namespace PolyInstall.Conditions;

/// <summary>
/// Small registry for <c>require</c> strings on tasks (e.g. <c>os.isWindows</c>). Extend deliberately; no arbitrary scripting.
/// </summary>
public static class ConditionEvaluator
{
    public static bool Evaluate(string? require)
    {
        if (string.IsNullOrWhiteSpace(require))
            return true;

        return require.Trim().ToLowerInvariant() switch
        {
            "os.iswindows" or "os.is_windows" => OperatingSystem.IsWindows(),
            "os.islinux" or "os.is_linux" => OperatingSystem.IsLinux(),
            "os.isosx" or "os.is_osx" or "os.ismacos" or "os.is_macos" => OperatingSystem.IsMacOS(),
            "os.isunix" or "os.is_unix" => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            _ => throw new NotSupportedException($"Unknown require condition: '{require}'. Supported: os.isWindows, os.isLinux, os.isOSX/os.isMacOS, os.isUnix."),
        };
    }
}
