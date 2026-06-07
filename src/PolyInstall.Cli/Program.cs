using System.Reflection;
using System.Text.Json;
using PolyInstall.Cli.Build;
using PolyInstall.Cli.Validation;
using PolyInstall.Core.Build.Manifest;
using PolyInstall.Core.Build.Validation;
using PolyInstall.Manifest;

static void Usage()
{
    Console.WriteLine("""
        polyinstall --version
        polyinstall build <manifest.yaml> [--base <dir>] [--stubs <dir>] [--verbose] [--json] [--output-manifest <file>]
        polyinstall validate <manifest.yaml> [--base <dir>]
        """);
}

static string GetCliVersion()
{
    var asm = Assembly.GetExecutingAssembly();
    var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    if (!string.IsNullOrEmpty(informational))
        return informational;
    return asm.GetName().Version?.ToString() ?? "0.0.0";
}

static string? Take(ref int i, string[] args)
{
    if (i + 1 >= args.Length)
        return null;
    return args[++i];
}

try
{
    if (args.Length == 0)
    {
        Usage();
        return 1;
    }

    if (args[0].Equals("--version", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine(GetCliVersion());
        return 0;
    }

    var verb = args[0].ToLowerInvariant();
    if (verb is "-h" or "--help")
    {
        Usage();
        return 0;
    }

    if (args.Length == 1)
    {
        Usage();
        return 1;
    }

    if (args[1] is "-h" or "--help")
    {
        Usage();
        return 0;
    }

    var manifestPath = Path.GetFullPath(args[1]);
    string? baseDir = null;
    string? stubsDir = null;
    var verbose = false;
    var jsonFlag = false;
    string? outputManifestPath = null;
    for (var i = 2; i < args.Length; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--base":
                baseDir = Take(ref i, args);
                break;
            case "--stubs":
                stubsDir = Take(ref i, args);
                break;
            case "--verbose":
            case "-v":
                verbose = true;
                break;
            case "--json":
                jsonFlag = true;
                break;
            case "--output-manifest":
                outputManifestPath = Take(ref i, args);
                break;
            default:
                Console.Error.WriteLine($"Unknown option: {args[i]}");
                return 1;
        }
    }

    var basePath = baseDir ?? Path.GetDirectoryName(manifestPath) ?? Directory.GetCurrentDirectory();

    switch (verb)
    {
        case "validate":
            {
                var yaml = await File.ReadAllTextAsync(manifestPath);
                var m = ManifestYaml.Parse(yaml);
                m = EnvironmentSubstitution.ApplyToManifest(m);
                var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema", "v1.json");
                if (!File.Exists(schemaPath))
                {
                    var dir = new DirectoryInfo(AppContext.BaseDirectory);
                    while (dir is not null)
                    {
                        var c = Path.Combine(dir.FullName, "schema", "v1.json");
                        if (File.Exists(c))
                        {
                            schemaPath = c;
                            break;
                        }
                        dir = dir.Parent;
                    }
                }
                if (!File.Exists(schemaPath))
                    throw new FileNotFoundException("Could not find schema/v1.json (looked next to CLI and parent directories).");
                var json = JsonSerializer.Serialize(m, InstallManifest.JsonOptions);
                ManifestJsonValidator.Validate(json, schemaPath);
                ManifestSemanticValidator.Validate(m);
                Console.WriteLine("Manifest is valid.");
                return 0;
            }
        case "build":
            BuildLog.Verbose = verbose;
            BuildLog.Quiet = jsonFlag || !string.IsNullOrEmpty(outputManifestPath);
            var result = await InstallerBuildPipeline.RunAsync(manifestPath, basePath, stubsDir, default);
            if (jsonFlag || !string.IsNullOrEmpty(outputManifestPath))
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = true,
                };
                var json = JsonSerializer.Serialize(result, jsonOptions);
                if (jsonFlag)
                    Console.WriteLine(json);
                if (!string.IsNullOrEmpty(outputManifestPath))
                    await File.WriteAllTextAsync(outputManifestPath, json);
            }
            return 0;
        default:
            Usage();
            return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    if (ex.InnerException is not null)
        Console.Error.WriteLine(ex.InnerException.Message);
    return 1;
}
