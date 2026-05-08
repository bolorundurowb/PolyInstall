using System.Text.Json.Serialization;
using PolyInstall.Core.Install;

namespace PolyInstall.Core.Manifest;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(InstallManifest))]
[JsonSerializable(typeof(InstallStateDocument))]
internal sealed partial class InstallJsonContext : JsonSerializerContext
{
}
