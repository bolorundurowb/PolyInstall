namespace PolyInstall.Core.Manifest;

public sealed class TasksConfiguration
{
    public List<InstallTask>? PreInstall { get; set; }
    public List<InstallTask>? PostInstall { get; set; }
    public List<InstallTask>? PreUninstall { get; set; }
    public List<InstallTask>? PostUninstall { get; set; }
}

public sealed class InstallTask
{
    public string? Require { get; set; }
    public string Action { get; set; } = "";
    public Dictionary<string, object?>? Parameters { get; set; }
}
