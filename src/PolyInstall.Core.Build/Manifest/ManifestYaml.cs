using PolyInstall.Manifest;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PolyInstall.Core.Build.Manifest;

public static class ManifestYaml
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Parses YAML into <see cref="InstallManifest"/> (snake_case keys).
    /// </summary>
    public static InstallManifest Parse(string yaml)
    {
        var intermediate = Deserializer.Deserialize<YamlManifestDto>(yaml)
                           ?? throw new InvalidOperationException("Empty or invalid YAML manifest.");
        return DtoToManifest(intermediate);
    }

    private static InstallManifest DtoToManifest(YamlManifestDto dto)
    {
        var m = new InstallManifest
        {
            Metadata = dto.Metadata ?? new ManifestMetadata(),
            Build = dto.Build ?? new BuildConfiguration(),
            Ui = dto.Ui ?? new UiConfiguration(),
            Files = dto.Files ?? [],
            FileAssociations = dto.FileAssociations,
            Tasks = dto.Tasks,
        };
        return m;
    }

    private sealed class YamlManifestDto
    {
        public ManifestMetadata? Metadata { get; set; }
        public BuildConfiguration? Build { get; set; }
        public UiConfiguration? Ui { get; set; }
        public List<FilesEntry>? Files { get; set; }
        public List<FileAssociation>? FileAssociations { get; set; }
        public TasksConfiguration? Tasks { get; set; }
    }
}
