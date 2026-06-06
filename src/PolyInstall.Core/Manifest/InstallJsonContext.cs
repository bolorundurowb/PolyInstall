using System.Text.Json;
using System.Text.Json.Serialization;
using PolyInstall.Install;

namespace PolyInstall.Manifest;

/// <summary>
/// Source-generated JSON serialization for persisted install state and embedded manifest on disk
/// (<c>install-state.json</c>, <c>embedded-manifest.json</c>).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(InstallManifest))]
[JsonSerializable(typeof(InstallStateDocument))]
[JsonSerializable(typeof(FileAssociationBackup))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class InstallJsonContext : JsonSerializerContext
{
}
