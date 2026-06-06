using System.Text;
using PolyInstall.Install;

namespace PolyInstall.Pal;

internal sealed class LinuxSystemdServiceManagerPal(ICommandRunner? runner = null) : IServiceManagerPal
{
    private readonly ICommandRunner _runner = runner ?? new CommandRunner();
    private readonly List<RegisteredServiceInfo> _registeredServices = [];

    public IReadOnlyList<RegisteredServiceInfo> RegisteredServices => _registeredServices;

    public void InstallOrUpdate(ServiceRegistrationInfo service)
    {
        var unitName = NormalizeUnitName(service.Name);
        var unitPath = GetUnitPath(service.Scope, unitName);

        if (IsSystemScope(service.Scope) && !PosixIdentity.IsRoot)
            throw new InvalidOperationException("Installing a system systemd service requires root privileges.");

        Directory.CreateDirectory(Path.GetDirectoryName(unitPath)!);
        File.WriteAllText(unitPath, BuildUnitContent(service));

        Systemctl(service.Scope, "daemon-reload");
        Systemctl(service.Scope, service.Enabled ? "enable" : "disable", unitName);
        if (service.Start)
            Systemctl(service.Scope, "start", unitName);

        _registeredServices.RemoveAll(s => ServiceKeyEquals(s, service.Name, service.Scope, "linux"));
        _registeredServices.Add(new RegisteredServiceInfo
        {
            Name = service.Name,
            Scope = NormalizeScope(service.Scope),
            Platform = "linux",
            UnitPath = unitPath,
            Enabled = service.Enabled,
            Started = service.Start,
        });
    }

    public void Remove(RegisteredServiceInfo service)
    {
        var unitName = NormalizeUnitName(service.Name);
        SystemctlBestEffort(service.Scope, "stop", unitName);
        SystemctlBestEffort(service.Scope, "disable", unitName);

        var unitPath = service.UnitPath ?? GetUnitPath(service.Scope, unitName);
        if (File.Exists(unitPath))
            File.Delete(unitPath);

        SystemctlBestEffort(service.Scope, "daemon-reload");
    }

    internal static string BuildUnitContent(ServiceRegistrationInfo service)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Unit]");
        sb.AppendLine($"Description={EscapeSystemdValue(service.Description ?? service.DisplayName ?? service.Name)}");
        sb.AppendLine();
        sb.AppendLine("[Service]");
        sb.AppendLine($"ExecStart={BuildExecStart(service)}");

        if (!string.IsNullOrWhiteSpace(service.WorkingDirectory))
            sb.AppendLine($"WorkingDirectory={EscapeSystemdValue(service.WorkingDirectory)}");

        if (!string.IsNullOrWhiteSpace(service.Restart))
            sb.AppendLine($"Restart={service.Restart}");

        if (service.Environment is { Count: > 0 })
        {
            foreach (var (key, value) in service.Environment.OrderBy(e => e.Key, StringComparer.Ordinal))
                sb.AppendLine($"Environment={QuoteSystemdEnvironment(key, value)}");
        }

        sb.AppendLine();
        sb.AppendLine("[Install]");
        sb.AppendLine(IsSystemScope(service.Scope) ? "WantedBy=multi-user.target" : "WantedBy=default.target");
        return sb.ToString();
    }

    internal static string NormalizeUnitName(string name) =>
        name.EndsWith(".service", StringComparison.OrdinalIgnoreCase) ? name : name + ".service";

    private static string GetUnitPath(string scope, string unitName)
    {
        if (IsSystemScope(scope))
            return Path.Combine("/etc/systemd/system", unitName);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "systemd", "user", unitName);
    }

    private void Systemctl(string scope, params string[] args)
    {
        var finalArgs = BuildSystemctlArgs(scope, args);
        _runner.Run("systemctl", finalArgs);
    }

    private void SystemctlBestEffort(string scope, params string[] args)
    {
        var finalArgs = BuildSystemctlArgs(scope, args);
        _runner.Run("systemctl", finalArgs, throwOnError: false);
    }

    private static string[] BuildSystemctlArgs(string scope, IReadOnlyCollection<string> args)
    {
        if (IsSystemScope(scope))
            return args.ToArray();

        return ["--user", .. args];
    }

    private static bool ServiceKeyEquals(RegisteredServiceInfo service, string name, string scope, string platform) =>
        service.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        && service.Scope.Equals(NormalizeScope(scope), StringComparison.OrdinalIgnoreCase)
        && service.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase);

    private static bool IsSystemScope(string scope) =>
        scope.Equals("system", StringComparison.OrdinalIgnoreCase)
        || scope.Equals("machine", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeScope(string scope) => IsSystemScope(scope) ? "system" : "user";

    private static string BuildExecStart(ServiceRegistrationInfo service)
    {
        var args = new[] { service.Executable }.Concat(service.Arguments);
        return string.Join(" ", args.Select(EscapeSystemdArgument));
    }

    private static string EscapeSystemdArgument(string value)
    {
        if (value.Length == 0)
            return "\"\"";
        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"') && !value.Contains('\\'))
            return value;
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string EscapeSystemdValue(string value) => value.Replace("\n", " ").Replace("\r", " ");

    private static string QuoteSystemdEnvironment(string key, string value) =>
        "\"" + key.Replace("\"", "\\\"") + "=" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
