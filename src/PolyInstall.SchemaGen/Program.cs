using System.Text.Json;
using System.Text.Json.Nodes;
using NJsonSchema.Generation;
using PolyInstall.Manifest;

var repoRoot = FindRepoRoot();
var outDir = Path.Combine(repoRoot, "schema");
Directory.CreateDirectory(outDir);
var outPath = Path.Combine(outDir, "v1.json");

var settings = new SystemTextJsonSchemaGeneratorSettings
{
    SerializerOptions = InstallManifest.JsonOptions,
};
var generator = new JsonSchemaGenerator(settings);
var schema = generator.Generate(typeof(InstallManifest));
var json = schema.ToJson();
var node = JsonNode.Parse(json)!.AsObject();
node["$schema"] = "https://json-schema.org/draft/2020-12/schema";
var final = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(outPath, final);
Console.WriteLine($"Wrote {outPath}");

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "src", "PolyInstall.slnx")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return Directory.GetCurrentDirectory();
}
