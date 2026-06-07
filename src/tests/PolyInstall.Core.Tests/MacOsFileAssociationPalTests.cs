using System.Xml.Linq;
using PolyInstall.Hosting;
using PolyInstall.Install;
using PolyInstall.Pal;

namespace PolyInstall.Core.Tests;

[Collection("Sequential")]
public class MacOsFileAssociationPalTests
{
    [Fact]
    public void ResolveUti_WithExplicitMimeType_ReturnsIt()
    {
        var assoc = new FileAssociationInfo
        {
            MimeType = "com.example.custom",
            ProgId = "MyApp",
        };

        var result = MacOsFileAssociationPal.ResolveUti(assoc);

        result.Verify().ToBe("com.example.custom");
    }

    [Fact]
    public void ResolveUti_WithoutExplicitMimeType_GeneratesFromProgId()
    {
        var assoc = new FileAssociationInfo
        {
            ProgId = "MyApp-v1.0",
        };

        var result = MacOsFileAssociationPal.ResolveUti(assoc);

        result.Verify().ToBe("myapp-v1.0");
    }

    [Fact]
    public void GetInfoPlistPath_ReturnsExpectedPath()
    {
        if (!OperatingSystem.IsMacOS())
            return; // macOS-only path layout; on Windows Path.Combine emits backslashes.

        MacOsFileAssociationPal.GetInfoPlistPath("/Applications/MyApp.app")
            .Verify().ToBe("/Applications/MyApp.app/Contents/Info.plist");
    }

    [Fact]
    public void GetOrCreateArray_ExistingKey_ReturnsExisting()
    {
        var dict = new XElement("dict",
            new XElement("key", "MyKey"),
            new XElement("array",
                new XElement("string", "existing")));

        var result = MacOsFileAssociationPal.GetOrCreateArray(dict, "MyKey");

        result.Verify().NotToBeNull();
        var values = result.Elements("string").Select(e => e.Value).ToList();
        values.Verify().ToHaveCount(1);
        values[0].Verify().ToBe("existing");
    }

    [Fact]
    public void GetOrCreateArray_NewKey_CreatesAndReturnsNew()
    {
        var dict = new XElement("dict");

        var result = MacOsFileAssociationPal.GetOrCreateArray(dict, "NewKey");

        result.Verify().NotToBeNull();
        result.Name.LocalName.Verify().ToBe("array");
        dict.Elements("key").Select(e => e.Value).Verify().ToContain("NewKey");
    }

    [Fact]
    public void FindDictInArray_Found_ReturnsDict()
    {
        var array = new XElement("array",
            new XElement("dict",
                new XElement("key", "CFBundleTypeName"),
                new XElement("string", "MyApp")));

        var result = MacOsFileAssociationPal.FindDictInArray(array, "CFBundleTypeName", "MyApp");

        result.Verify().NotToBeNull();
    }

    [Fact]
    public void FindDictInArray_NotFound_ReturnsNull()
    {
        var array = new XElement("array",
            new XElement("dict",
                new XElement("key", "CFBundleTypeName"),
                new XElement("string", "OtherApp")));

        var result = MacOsFileAssociationPal.FindDictInArray(array, "CFBundleTypeName", "MyApp");

        result.Verify().ToBeNull();
    }

    [Fact]
    public void UpdateStringInDict_NoAppend_UpdatesExisting()
    {
        var dict = new XElement("dict",
            new XElement("key", "Name"),
            new XElement("string", "Old"));

        MacOsFileAssociationPal.UpdateStringInDict(dict, "Name", "New", append: false);

        var values = dict.Elements("string").Select(e => e.Value).ToList();
        values.Verify().ToHaveCount(1);
        values[0].Verify().ToBe("New");
    }

    [Fact]
    public void UpdateStringInDict_NoAppend_CreatesNew()
    {
        var dict = new XElement("dict");

        MacOsFileAssociationPal.UpdateStringInDict(dict, "Name", "New", append: false);

        dict.Elements("key").Select(e => e.Value).Verify().ToContain("Name");
        dict.Elements("string").Select(e => e.Value).Verify().ToContain("New");
    }

    [Fact]
    public void UpdateStringInDict_Append_AddsToArray()
    {
        var dict = new XElement("dict",
            new XElement("key", "Exts"),
            new XElement("array",
                new XElement("string", "old")));

        MacOsFileAssociationPal.UpdateStringInDict(dict, "Exts", "new", append: true);

        var values = dict.Elements("array").Elements("string").Select(e => e.Value).ToList();
        values.Verify().ToContain("old");
        values.Verify().ToContain("new");
    }

    [Fact]
    public void UpdateStringInDict_Append_CreatesNewArray()
    {
        var dict = new XElement("dict");

        MacOsFileAssociationPal.UpdateStringInDict(dict, "Exts", "new", append: true);

        var array = dict.Elements("array").FirstOrDefault();
        array.Verify().NotToBeNull();
        var values = array!.Elements("string").Select(e => e.Value).ToList();
        values.Verify().ToHaveCount(1);
        values[0].Verify().ToBe("new");
    }

    [Fact]
    public void CreateStringElement_ReturnsDictWithKeyAndString()
    {
        var result = MacOsFileAssociationPal.CreateStringElement("MyKey", "MyValue");

        result.Name.LocalName.Verify().ToBe("dict");
        result.Elements("key").Select(e => e.Value).Verify().ToContain("MyKey");
        result.Elements("string").Select(e => e.Value).Verify().ToContain("MyValue");
    }

    [Fact]
    public void CreateArrayElement_ReturnsPlaceholderWithKeyAndArray()
    {
        var result = MacOsFileAssociationPal.CreateArrayElement("MyKey", "MyValue");

        result.Name.LocalName.Verify().ToBe("placeholder");
        result.Elements("key").Select(e => e.Value).Verify().ToContain("MyKey");
        var values = result.Elements("array").Elements("string").Select(e => e.Value).ToList();
        values.Verify().ToHaveCount(1);
        values[0].Verify().ToBe("MyValue");
    }

    [Fact]
    public void IsKeyBefore_MatchingKey_ReturnsTrue()
    {
        var key = new XElement("key", "UTTypeIdentifier");

        MacOsFileAssociationPal.IsKeyBefore(key, "UTTypeIdentifier").Verify().ToBeTrue();
    }

    [Fact]
    public void IsKeyBefore_NonMatchingKey_ReturnsFalse()
    {
        var key = new XElement("key", "UTTypeIdentifier");

        MacOsFileAssociationPal.IsKeyBefore(key, "OtherKey").Verify().ToBeFalse();
    }

    [Fact]
    public void IsKeyBefore_NonKeyElement_ReturnsFalse()
    {
        var element = new XElement("string", "UTTypeIdentifier");

        MacOsFileAssociationPal.IsKeyBefore(element, "UTTypeIdentifier").Verify().ToBeFalse();
    }

    [Fact]
    public void AddOrUpdateDocumentType_NewEntry_AddsToArray()
    {
        var rootDict = new XElement("dict");
        var assoc = new FileAssociationInfo
        {
            ProgId = "MyApp",
            Description = "My Application",
        };

        MacOsFileAssociationPal.AddOrUpdateDocumentType(rootDict, assoc, "com.example.myapp", "myext");

        var array = MacOsFileAssociationPal.GetOrCreateArray(rootDict, "CFBundleDocumentTypes");
        array.Elements("dict").Verify().ToHaveCount(1);
    }

    [Fact]
    public void AddOrUpdateDocumentType_ExistingEntry_UpdatesExtensions()
    {
        var rootDict = new XElement("dict",
            new XElement("key", "CFBundleDocumentTypes"),
            new XElement("array",
                new XElement("dict",
                    new XElement("key", "CFBundleTypeName"),
                    new XElement("string", "MyApp"),
                    new XElement("key", "CFBundleTypeExtensions"),
                    new XElement("array",
                        new XElement("string", "old")))));

        var assoc = new FileAssociationInfo { ProgId = "MyApp" };

        MacOsFileAssociationPal.AddOrUpdateDocumentType(rootDict, assoc, "com.example.myapp", "new");

        var array = MacOsFileAssociationPal.GetOrCreateArray(rootDict, "CFBundleDocumentTypes");
        var entry = MacOsFileAssociationPal.FindDictInArray(array, "CFBundleTypeName", "MyApp");
        entry.Verify().NotToBeNull();
        var extArray = entry!.Elements()
            .FirstOrDefault(e => e.Name == "key" && e.Value == "CFBundleTypeExtensions")
            ?.ElementsAfterSelf("array").FirstOrDefault();
        extArray.Verify().NotToBeNull();
        extArray!.Elements("string").Select(e => e.Value).Verify().ToContain("old");
        extArray.Elements("string").Select(e => e.Value).Verify().ToContain("new");
    }

    [Fact]
    public void AddOrUpdateTypeDeclaration_NewEntry_AddsToArray()
    {
        var rootDict = new XElement("dict");
        var assoc = new FileAssociationInfo
        {
            ProgId = "MyApp",
            Description = "My Application",
        };

        MacOsFileAssociationPal.AddOrUpdateTypeDeclaration(rootDict, assoc, "com.example.myapp", "myext");

        var array = MacOsFileAssociationPal.GetOrCreateArray(rootDict, "UTExportedTypeDeclarations");
        array.Elements("dict").Verify().ToHaveCount(1);
    }

    [Fact]
    public void AddOrUpdateTypeDeclaration_ExistingEntry_UpdatesTagSpec()
    {
        var rootDict = new XElement("dict",
            new XElement("key", "UTExportedTypeDeclarations"),
            new XElement("array",
                new XElement("dict",
                    new XElement("key", "UTTypeIdentifier"),
                    new XElement("string", "com.example.myapp"),
                    new XElement("key", "UTTypeTagSpecification"),
                    new XElement("dict",
                        new XElement("key", "public.filename-extension"),
                        new XElement("array",
                            new XElement("string", "old"))))));

        var assoc = new FileAssociationInfo { ProgId = "MyApp" };

        MacOsFileAssociationPal.AddOrUpdateTypeDeclaration(rootDict, assoc, "com.example.myapp", "new");

        var array = MacOsFileAssociationPal.GetOrCreateArray(rootDict, "UTExportedTypeDeclarations");
        var entry = MacOsFileAssociationPal.FindDictInArray(array, "UTTypeIdentifier", "com.example.myapp");
        entry.Verify().NotToBeNull();
    }

    [Fact]
    public void GetBackup_WhenNoInstallDirectory_ReturnsNull()
    {
        var original = InstallBootstrap.InstallDirectory;
        try
        {
            InstallBootstrap.InstallDirectory = null;
            var result = MacOsFileAssociationPal.GetBackup(".test");
            result.Verify().ToBeNull();
        }
        finally
        {
            InstallBootstrap.InstallDirectory = original;
        }
    }
}
