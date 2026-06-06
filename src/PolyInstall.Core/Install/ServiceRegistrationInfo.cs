namespace PolyInstall.Install;

public sealed class ServiceRegistrationInfo
{
    public string Name { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string Scope { get; set; } = "system";
    public bool Enabled { get; set; } = true;
    public bool Start { get; set; }
    public string Executable { get; set; } = "";
    public List<string> Arguments { get; set; } = [];
    public string? WorkingDirectory { get; set; }
    public string? Restart { get; set; }
    public Dictionary<string, string>? Environment { get; set; }
}

public sealed class RegisteredServiceInfo
{
    public string Name { get; set; } = "";
    public string Scope { get; set; } = "system";
    public string Platform { get; set; } = "";
    public string? UnitPath { get; set; }
    public bool Enabled { get; set; }
    public bool Started { get; set; }
}
