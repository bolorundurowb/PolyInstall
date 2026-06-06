using System.ComponentModel;

namespace PolyInstall.Manifest;

public sealed class FileAssociation
{
    [Description("The file extension, including the leading dot (e.g., '.oef').")]
    public string Extension { get; set; } = "";

    [Description("A brief description of the file type.")]
    public string Description { get; set; } = "";
    
    [Description("Optional: The ProgID for the file association (e.g., 'MyApp.oef.1'). If omitted, one will be generated based on the application name and extension.")]
    public string? ProgId { get; set; }

    [Description("The path to the icon file for this file type, relative to the install directory.")]
    public string? Icon { get; set; }

    [Description("The command to execute when opening a file of this type. Use '%1' as a placeholder for the file path.")]
    public string Command { get; set; } = "";

    [Description("Optional: The MIME type for this file association (Linux only). If omitted, one will be derived from the extension (e.g., '.oef' -> 'application/x-oef').")]
    public string? MimeType { get; set; }

    [Description("macOS only: Path to the .app bundle to register associations for.")]
    public string? BundlePath { get; set; }
}
