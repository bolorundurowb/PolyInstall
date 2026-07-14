using PolyInstall.Core.Build.Manifest;
using PolyInstall.Core.Build.Validation;
using PolyInstall.Manifest;
using Assert = Xunit.Assert;

namespace PolyInstall.Core.Build.Tests;

public class FeatureManifestTests
{
    [Fact]
    public void Parse_WithFeatures_MapsFieldsAndReferences()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            build:
              targets:
                - windows-x64
            features:
              - id: simulator
                name: Simulator
                description: Sim runtime
                default_selected: true
              - id: samples
                name: Samples
                description: Sample files
                default_selected: false
            files:
              - source_dir: .
                include:
                  - "core/**/*"
              - source_dir: .
                include:
                  - "sim/**/*"
                features: [simulator]
              - source_dir: .
                include:
                  - "samples/**/*"
                features: [samples]
            tasks:
              post_install:
                - action: create_shortcut
                  require: os.isWindows
                  features: [simulator]
                  parameters:
                    target_path: "{AppDir}/sim.exe"
                    name: "Sim"
                    location: start_menu
            file_associations:
              - extension: .sim
                description: Simulator file
                command: "open %1"
                features: [simulator]
            """;

        var m = ManifestYaml.Parse(yaml);

        m.Features.Must().HaveCount(2);
        m.Features![0].Id.Must().Be("simulator");
        m.Features[0].Name.Must().Be("Simulator");
        m.Features[0].DefaultSelected.Must().BeTrue();
        m.Features[1].Id.Must().Be("samples");
        m.Features[1].DefaultSelected.Must().BeFalse();

        m.Files[1].Features.Must().BeEquivalentTo(["simulator"]);
        m.Files[2].Features.Must().BeEquivalentTo(["samples"]);
        m.Tasks!.PostInstall![0].Features.Must().BeEquivalentTo(["simulator"]);
        m.FileAssociations![0].Features.Must().BeEquivalentTo(["simulator"]);
    }

    [Fact]
    public void Validate_DuplicateFeatureId_Throws()
    {
        var manifest = CreateBaseManifest();
        manifest.Features =
        [
            new FeatureDefinition { Id = "x", Name = "X" },
            new FeatureDefinition { Id = "x", Name = "X again" },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Must().Contain("duplicated");
    }

    [Fact]
    public void Validate_EmptyFeatureId_Throws()
    {
        var manifest = CreateBaseManifest();
        manifest.Features =
        [
            new FeatureDefinition { Id = "", Name = "Bad" },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Must().Contain("features[0].id");
    }

    [Fact]
    public void Validate_FilesEntryReferencesUnknownFeature_Throws()
    {
        var manifest = CreateBaseManifest();
        manifest.Features = [new FeatureDefinition { Id = "known", Name = "Known" }];
        manifest.Files =
        [
            new FilesEntry { SourceDir = ".", Include = ["*.txt"] },
            new FilesEntry { SourceDir = ".", Include = ["*.dat"], Features = ["ghost"] },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Must().Contain("files[1].features references unknown feature id 'ghost'");
    }

    [Fact]
    public void Validate_TaskReferencesUnknownFeature_Throws()
    {
        var manifest = CreateBaseManifest();
        manifest.Features = [new FeatureDefinition { Id = "known", Name = "Known" }];
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Features = ["ghost"],
                    Parameters = new Dictionary<string, object?>
                    {
                        ["target_path"] = "app.exe",
                        ["name"] = "app",
                        ["location"] = "desktop",
                    },
                },
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Must().Contain("tasks.post_install[0].features references unknown feature id 'ghost'");
    }

    [Fact]
    public void Validate_FileAssociationReferencesUnknownFeature_Throws()
    {
        var manifest = CreateBaseManifest();
        manifest.Features = [new FeatureDefinition { Id = "known", Name = "Known" }];
        manifest.FileAssociations =
        [
            new FileAssociation
            {
                Extension = ".sim",
                Description = "Sim",
                Command = "app %1",
                Features = ["ghost"],
            },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Must().Contain("file_associations[0].features references unknown feature id 'ghost'");
    }

    [Fact]
    public void Validate_FilesReferenceFeatureWithoutDefinitions_Throws()
    {
        var manifest = CreateBaseManifest();
        manifest.Features = null;
        manifest.Files =
        [
            new FilesEntry { SourceDir = ".", Include = ["*.txt"], Features = ["mystery"] },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Must().Contain("no features are defined");
    }

    [Fact]
    public void Validate_WizardFeaturesBeforeDestination_Throws()
    {
        var manifest = CreateBaseManifest();
        manifest.Ui.WizardSteps =
        [
            new WizardStep { Type = "welcome" },
            new WizardStep { Type = "features" },
            new WizardStep { Type = "destination" },
            new WizardStep { Type = "progress" },
            new WizardStep { Type = "finish" },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Must().Contain("'features' before 'destination'");
    }

    [Fact]
    public void Validate_WizardFeaturesAfterProgress_Throws()
    {
        var manifest = CreateBaseManifest();
        manifest.Ui.WizardSteps =
        [
            new WizardStep { Type = "welcome" },
            new WizardStep { Type = "destination" },
            new WizardStep { Type = "progress" },
            new WizardStep { Type = "features" },
            new WizardStep { Type = "finish" },
        ];

        var ex = Assert.Throws<InvalidOperationException>(() => ManifestSemanticValidator.Validate(manifest));
        ex.Message.Must().Contain("'features' after 'progress'");
    }

    [Fact]
    public void Validate_WizardFeaturesBetweenDestinationAndProgress_Passes()
    {
        var manifest = CreateBaseManifest();
        manifest.Features = [new FeatureDefinition { Id = "f", Name = "Feature" }];
        manifest.Ui.WizardSteps =
        [
            new WizardStep { Type = "welcome" },
            new WizardStep { Type = "destination" },
            new WizardStep { Type = "features" },
            new WizardStep { Type = "progress" },
            new WizardStep { Type = "finish" },
        ];

        ManifestSemanticValidator.Validate(manifest);
    }

    [Fact]
    public void Validate_AllReferencesValid_Passes()
    {
        var manifest = CreateBaseManifest();
        manifest.Features =
        [
            new FeatureDefinition { Id = "sim", Name = "Sim" },
            new FeatureDefinition { Id = "samples", Name = "Samples", DefaultSelected = false },
        ];
        manifest.Files =
        [
            new FilesEntry { SourceDir = ".", Include = ["core/**/*"] },
            new FilesEntry { SourceDir = ".", Include = ["sim/**/*"], Features = ["sim"] },
        ];

        ManifestSemanticValidator.Validate(manifest);
    }

    private static InstallManifest CreateBaseManifest()
    {
        return new InstallManifest
        {
            Metadata = new ManifestMetadata { Name = "Test", Version = "1.0.0" },
            Build = new BuildConfiguration
            {
                Targets = ["windows-x64"],
                Windows = new WindowsBuildOptions { InstallScope = "user" },
            },
            Ui = new UiConfiguration { WizardSteps = [] },
            Files = [new FilesEntry { SourceDir = ".", Include = ["*.txt"] }],
        };
    }
}
