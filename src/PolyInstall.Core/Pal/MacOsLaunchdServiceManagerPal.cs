using System.Text;
using System.Xml.Linq;
using PolyInstall.Install;

namespace PolyInstall.Pal;

internal sealed class MacOsLaunchdServiceManagerPal(ICommandRunner? runner = null) : IServiceManagerPal
{
    private readonly ICommandRunner _runner = runner ?? new CommandRunner();
    private readonly List<RegisteredServiceInfo> _registeredServices = [];

    public IReadOnlyList<RegisteredServiceInfo> RegisteredServices => _registeredServices;

    public void InstallOrUpdate(ServiceRegistrationInfo service)
    {
        var label = service.Name;
        var plistPath = GetPlistPath(service.Scope, label);

        if (IsSystemScope(service.Scope) && !PosixIdentity.IsRoot)
            throw new InvalidOperationException("Installing a system launchd daemon requires root privileges.");

        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
        File.WriteAllText(plistPath, BuildPlistContent(service));

        if (IsSystemScope(service.Scope))
        {
            _runner.Run("chown", ["root:wheel", plistPath]);
            _runner.Run("chmod", ["0644", plistPath]);
        }

        if (service.Enabled)
        {
            LaunchctlBestEffort("bootout", Target(service.Scope), plistPath);
            Launchctl("bootstrap", Target(service.Scope), plistPath);
            LaunchctlBestEffort("enable", ServiceTarget(service.Scope, label));
            if (service.Start)
                LaunchctlBestEffort("kickstart", "-k", ServiceTarget(service.Scope, label));
        }
        else
        {
            LaunchctlBestEffort("bootout", Target(service.Scope), plistPath);
            LaunchctlBestEffort("disable", ServiceTarget(service.Scope, label));
        }

        _registeredServices.RemoveAll(s => ServiceKeyEquals(s, service.Name, service.Scope, "macos"));
        _registeredServices.Add(new RegisteredServiceInfo
        {
            Name = service.Name,
            Scope = NormalizeScope(service.Scope),
            Platform = "macos",
            UnitPath = plistPath,
            Enabled = service.Enabled,
            Started = service.Start,
        });
    }

    public void Remove(RegisteredServiceInfo service)
    {
        // Install state is user-writable; never let it aim privileged actions at plists that
        // were not installed from this install root.
        if (!Manifest.RuntimeManifestGuard.IsValidServiceName(service.Name))
            return;

        var label = service.Name;
        // The plist path is recomputed from name+scope; the state-provided UnitPath is not trusted.
        var plistPath = GetPlistPath(service.Scope, label);
        if (IsSystemScope(service.Scope) && !PlistReferencesInstallRoot(plistPath))
            return;

        LaunchctlBestEffort("bootout", Target(service.Scope), plistPath);
        LaunchctlBestEffort("disable", ServiceTarget(service.Scope, label));

        if (File.Exists(plistPath))
            File.Delete(plistPath);
    }

    private static bool PlistReferencesInstallRoot(string plistPath)
    {
        var installRoot = Hosting.InstallBootstrap.InstallDirectory;
        if (string.IsNullOrWhiteSpace(installRoot) || !File.Exists(plistPath))
            return false;

        try
        {
            var content = File.ReadAllText(plistPath);
            return content.Contains(Path.GetFullPath(installRoot), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    internal static string BuildPlistContent(ServiceRegistrationInfo service)
    {
        var dict = new XElement("dict",
            KeyValue("Label", service.Name),
            KeyArray("ProgramArguments", new[] { service.Executable }.Concat(service.Arguments)));

        if (!string.IsNullOrWhiteSpace(service.WorkingDirectory))
            dict.Add(KeyValue("WorkingDirectory", service.WorkingDirectory));

        if (!service.Enabled)
            dict.Add(KeyBool("Disabled", true));

        if (ShouldKeepAlive(service.Restart))
            dict.Add(KeyBool("KeepAlive", true));

        if (service.Environment is { Count: > 0 })
        {
            dict.Add(new XElement("key", "EnvironmentVariables"));
            var envDict = new XElement("dict");
            foreach (var (key, value) in service.Environment.OrderBy(e => e.Key, StringComparer.Ordinal))
                envDict.Add(KeyValue(key, value));
            dict.Add(envDict);
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
            new XElement("plist", new XAttribute("version", "1.0"), dict));

        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        doc.Save(writer);
        return sb.ToString();
    }

    private static string GetPlistPath(string scope, string label)
    {
        var fileName = label.EndsWith(".plist", StringComparison.OrdinalIgnoreCase) ? label : label + ".plist";
        if (IsSystemScope(scope))
            return Path.Combine("/Library/LaunchDaemons", fileName);

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, "Library", "LaunchAgents", fileName);
    }

    private void Launchctl(string command, params string[] args) =>
        _runner.Run("launchctl", [command, .. args]);

    private void LaunchctlBestEffort(string command, params string[] args) =>
        _runner.Run("launchctl", [command, .. args], throwOnError: false);

    private static string Target(string scope) =>
        IsSystemScope(scope) ? "system" : $"gui/{PosixIdentity.UserId}";

    private static string ServiceTarget(string scope, string label) => $"{Target(scope)}/{label}";

    private static bool ServiceKeyEquals(RegisteredServiceInfo service, string name, string scope, string platform) =>
        service.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        && service.Scope.Equals(NormalizeScope(scope), StringComparison.OrdinalIgnoreCase)
        && service.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase);

    private static bool IsSystemScope(string scope) =>
        scope.Equals("system", StringComparison.OrdinalIgnoreCase)
        || scope.Equals("machine", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeScope(string scope) => IsSystemScope(scope) ? "system" : "user";

    private static bool ShouldKeepAlive(string? restart) =>
        restart is not null
        && (restart.Equals("always", StringComparison.OrdinalIgnoreCase)
            || restart.Equals("on-failure", StringComparison.OrdinalIgnoreCase));

    private static object[] KeyValue(string key, string value) => [new XElement("key", key), new XElement("string", value)];

    private static object[] KeyArray(string key, IEnumerable<string> values) =>
    [
        new XElement("key", key),
        new XElement("array", values.Select(v => new XElement("string", v)))
    ];

    private static object[] KeyBool(string key, bool value) =>
    [
        new XElement("key", key),
        new XElement(value ? "true" : "false")
    ];
}
