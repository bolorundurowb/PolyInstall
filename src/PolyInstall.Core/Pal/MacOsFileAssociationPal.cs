using System.Diagnostics;
using System.Runtime.Versioning;
using System.Xml.Linq;
using PolyInstall.Hosting;
using PolyInstall.Install;

namespace PolyInstall.Pal;

internal sealed class MacOsFileAssociationPal : IFileAssociationPal
{
    private static readonly XNamespace PlistNs = "";

    [SupportedOSPlatform("macos")]
    public void Register(FileAssociationInfo association)
    {
        if (string.IsNullOrEmpty(association.BundlePath))
            throw new InvalidOperationException("file_association requires 'bundle_path' on macOS.");

        var infoPlistPath = GetInfoPlistPath(association.BundlePath);
        if (!File.Exists(infoPlistPath))
            throw new FileNotFoundException($"Info.plist not found at '{infoPlistPath}'.");

        Backup(association.Extension, infoPlistPath);

        var plist = XDocument.Load(infoPlistPath);
        var rootDict = plist.Root!.Element("dict")
            ?? throw new InvalidOperationException("Invalid Info.plist: missing root <dict>.");

        var uti = ResolveUti(association);
        var extWithoutDot = association.Extension.TrimStart('.');

        AddOrUpdateDocumentType(rootDict, association, uti, extWithoutDot);
        AddOrUpdateTypeDeclaration(rootDict, association, uti, extWithoutDot);

        plist.Save(infoPlistPath);
        ReRegisterBundle(association.BundlePath);
    }

    [SupportedOSPlatform("macos")]
    public void Unregister(FileAssociationInfo association)
    {
        if (string.IsNullOrEmpty(association.BundlePath))
            return;

        var infoPlistPath = GetInfoPlistPath(association.BundlePath);
        var backup = GetBackup(association.Extension);

        if (backup is not null && !string.IsNullOrEmpty(backup.OriginalInfoPlistContent))
        {
            File.WriteAllText(infoPlistPath, backup.OriginalInfoPlistContent);
        }

        ReRegisterBundle(association.BundlePath);
    }

    private static string GetInfoPlistPath(string bundlePath)
        => Path.Combine(bundlePath, "Contents", "Info.plist");

    private static string ResolveUti(FileAssociationInfo association)
    {
        if (!string.IsNullOrEmpty(association.MimeType))
            return association.MimeType;

        var safeProgId = new string(association.ProgId
            .Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '-')
            .ToArray())
            .ToLowerInvariant();
        return safeProgId;
    }

    [SupportedOSPlatform("macos")]
    private static void Backup(string extension, string infoPlistPath)
    {
        var installDir = InstallBootstrap.InstallDirectory;
        if (string.IsNullOrEmpty(installDir)) return;

        try
        {
            var state = InstallStateIo.ReadState(installDir);
            state.FileAssociationBackups ??= new List<FileAssociationBackup>();

            if (state.FileAssociationBackups.Any(b => b.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                return;

            var originalContent = File.ReadAllText(infoPlistPath);
            state.FileAssociationBackups.Add(new FileAssociationBackup
            {
                Extension = extension,
                OriginalInfoPlistContent = originalContent,
            });
            InstallStateIo.WriteState(installDir, state);
        }
        catch { }
    }

    private static FileAssociationBackup? GetBackup(string extension)
    {
        var installDir = InstallBootstrap.InstallDirectory;
        if (string.IsNullOrEmpty(installDir)) return null;

        try
        {
            var state = InstallStateIo.ReadState(installDir);
            return state.FileAssociationBackups?.FirstOrDefault(b =>
                b.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static void AddOrUpdateDocumentType(
        XElement rootDict, FileAssociationInfo association, string uti, string extWithoutDot)
    {
        var docTypesArray = GetOrCreateArray(rootDict, "CFBundleDocumentTypes");

        var existingEntry = FindDictInArray(docTypesArray, "CFBundleTypeName", association.ProgId);
        if (existingEntry is not null)
        {
            UpdateStringInDict(existingEntry, "CFBundleTypeExtensions", extWithoutDot, append: true);
            UpdateStringInDict(existingEntry, "LSItemContentTypes", uti, append: true);
            return;
        }

        var newEntry = new XElement("dict",
            CreateStringElement("CFBundleTypeName", association.ProgId),
            CreateStringElement("CFBundleTypeRole", "Editor"),
            CreateArrayElement("CFBundleTypeExtensions", extWithoutDot),
            CreateArrayElement("LSItemContentTypes", uti));

        docTypesArray.Add(newEntry);
    }

    private static void AddOrUpdateTypeDeclaration(
        XElement rootDict, FileAssociationInfo association, string uti, string extWithoutDot)
    {
        var typeDeclsArray = GetOrCreateArray(rootDict, "UTExportedTypeDeclarations");

        var existingEntry = FindDictInArray(typeDeclsArray, "UTTypeIdentifier", uti);
        if (existingEntry is not null)
        {
            var tagSpec = existingEntry.Element("dict");
            if (tagSpec is not null)
            {
                var extArray = tagSpec.Elements()
                    .FirstOrDefault(e => IsKeyBefore(e, "public.filename-extension"));
                if (extArray is not null)
                {
                    var values = extArray.Elements("string").Select(e => e.Value).ToList();
                    if (!values.Contains(extWithoutDot, StringComparer.OrdinalIgnoreCase))
                        extArray.Add(new XElement("string", extWithoutDot));
                }
            }
            return;
        }

        var tagSpecDict = new XElement("dict",
            new XElement("key", "public.filename-extension"),
            new XElement("array", new XElement("string", extWithoutDot)));

        var newDecl = new XElement("dict",
            CreateStringElement("UTTypeIdentifier", uti),
            CreateStringElement("UTTypeDescription", association.Description),
            CreateArrayElement("UTTypeConformsTo", "public.data"),
            new XElement("key", "UTTypeTagSpecification"),
            tagSpecDict);

        typeDeclsArray.Add(newDecl);
    }

    private static XElement GetOrCreateArray(XElement parentDict, string key)
    {
        var existing = parentDict.Elements()
            .Where(e => e.Name == "key" && e.Value == key)
            .Select(e => e.ElementsAfterSelf("array").FirstOrDefault())
            .FirstOrDefault();

        if (existing is not null)
            return existing;

        var newArray = new XElement("array");
        parentDict.Add(new XElement("key", key));
        parentDict.Add(newArray);
        return newArray;
    }

    private static XElement? FindDictInArray(XElement array, string keyName, string keyValue)
    {
        return array.Elements("dict")
            .FirstOrDefault(d => d.Elements()
                .Any(e => e.Name == "key" && e.Value == keyName
                    && e.ElementsAfterSelf().FirstOrDefault()?.Value == keyValue));
    }

    private static void UpdateStringInDict(XElement dict, string key, string value, bool append)
    {
        if (!append)
        {
            var existing = dict.Elements()
                .Where(e => e.Name == "key" && e.Value == key)
                .Select(e => e.ElementsAfterSelf().FirstOrDefault())
                .FirstOrDefault();
            if (existing is not null)
            {
                existing.Value = value;
                return;
            }

            dict.Add(new XElement("key", key));
            dict.Add(new XElement("string", value));
            return;
        }

        var existingArray = dict.Elements()
            .Where(e => e.Name == "key" && e.Value == key)
            .Select(e => e.ElementsAfterSelf("array").FirstOrDefault())
            .FirstOrDefault();

        if (existingArray is not null)
        {
            var values = existingArray.Elements("string").Select(e => e.Value).ToList();
            if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
                existingArray.Add(new XElement("string", value));
            return;
        }

        dict.Add(new XElement("key", key));
        dict.Add(new XElement("array", new XElement("string", value)));
    }

    private static XElement CreateStringElement(string key, string value)
    {
        return new XElement("dict",
            new XElement("key", key),
            new XElement("string", value))
            .Elements().First().Parent!;
    }

    private static XElement CreateArrayElement(string key, string value)
    {
        return new XElement("placeholder",
            new XElement("key", key),
            new XElement("array", new XElement("string", value)))
            .Elements().First().Parent!;
    }

    private static bool IsKeyBefore(XElement element, string keyName)
    {
        return element.Name == "key" && element.Value == keyName;
    }

    [SupportedOSPlatform("macos")]
    private static void ReRegisterBundle(string bundlePath)
    {
        var lsregister = "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister";
        var psi = new ProcessStartInfo
        {
            FileName = lsregister,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(bundlePath);
        using var p = Process.Start(psi);
        p?.WaitForExit();
    }
}
