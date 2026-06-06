using PolyInstall.Install;

namespace PolyInstall.Pal;

internal sealed class WindowsServiceManagerPal(ICommandRunner? runner = null) : IServiceManagerPal
{
    private readonly ICommandRunner _runner = runner ?? new CommandRunner();
    private readonly List<RegisteredServiceInfo> _registeredServices = [];

    public IReadOnlyList<RegisteredServiceInfo> RegisteredServices => _registeredServices;

    public void InstallOrUpdate(ServiceRegistrationInfo service)
    {
        var binPath = BuildBinPath(service);
        var startMode = service.Enabled ? "auto" : "disabled";
        var displayName = service.DisplayName ?? service.Name;

        if (ServiceExists(service.Name))
        {
            Run("config", service.Name, "binPath=", binPath, "start=", startMode, "DisplayName=", displayName);
        }
        else
        {
            Run("create", service.Name, "binPath=", binPath, "start=", startMode, "DisplayName=", displayName);
        }

        if (!string.IsNullOrWhiteSpace(service.Description))
            Run("description", service.Name, service.Description);

        if (service.Start)
            RunBestEffort("start", service.Name);

        _registeredServices.RemoveAll(s => ServiceKeyEquals(s, service.Name, "system", "windows"));
        _registeredServices.Add(new RegisteredServiceInfo
        {
            Name = service.Name,
            Scope = "system",
            Platform = "windows",
            Enabled = service.Enabled,
            Started = service.Start,
        });
    }

    public void Remove(RegisteredServiceInfo service)
    {
        RunBestEffort("stop", service.Name);
        RunBestEffort("config", service.Name, "start=", "disabled");
        RunBestEffort("delete", service.Name);
    }

    internal static string BuildBinPath(ServiceRegistrationInfo service)
    {
        var parts = new[] { service.Executable }.Concat(service.Arguments);
        return string.Join(" ", parts.Select(QuoteForCommandLine));
    }

    private bool ServiceExists(string name) => RunBestEffort("query", name) == 0;

    private void Run(params string[] args) => _runner.Run("sc.exe", args);

    private int RunBestEffort(params string[] args) => _runner.Run("sc.exe", args, throwOnError: false);

    private static bool ServiceKeyEquals(RegisteredServiceInfo service, string name, string scope, string platform) =>
        service.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        && service.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase)
        && service.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase);

    private static string QuoteForCommandLine(string value)
    {
        if (value.Length == 0)
            return "\"\"";
        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"'))
            return value;

        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
