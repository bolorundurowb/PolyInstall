using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Defines a file association to be registered on the target operating system.
/// </summary>
public sealed class FileAssociation
{
    /// <summary>Gets or sets the file extension, including the leading dot (e.g., <c>.txt</c>).</summary>
    [Description("The file extension, including the leading dot (e.g., '.txt').")]
    public string Extension { get; set; } = "";

    /// <summary>Gets or sets a brief description of the file type.</summary>
    [Description("A brief description of the file type.")]
    public string Description { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional ProgID for the file association (e.g., <c>MyApp.oef.1</c>).
    /// If omitted, one will be generated based on the application name and extension.
    /// </summary>
    [Description("Optional ProgID for the file association (e.g., 'MyApp.oef.1'). If omitted, one will be generated based on the application name and extension.")]
    public string? ProgId { get; set; }

    /// <summary>Gets or sets the path to the icon file for this file type, relative to the installation directory.</summary>
    [Description("The path to the icon file for this file type, relative to the installation directory.")]
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the command to execute when opening a file of this type.
    /// Use <c>%1</c> as a placeholder for the file path.
    /// </summary>
    [Description("The command to execute when opening a file of this type. Use %1 as a placeholder for the file path.")]
    public string Command { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional MIME type for this file association (Linux only).
    /// If omitted, one will be derived from the extension.
    /// </summary>
    [Description("Optional MIME type for this file association (Linux only). If omitted, one will be derived from the extension.")]
    public string? MimeType { get; set; }

    /// <summary>Gets or sets the optional path to the .app bundle to register associations for (macOS only).</summary>
    [Description("Optional path to the .app bundle to register associations for (macOS only).")]
    public string? BundlePath { get; set; }

    /// <summary>
    /// Gets or sets the optional list of feature identifiers that gate this association.
    /// When null or empty, the association is always registered.
    /// </summary>
    [Description("Optional list of feature identifiers that gate this association. When null or empty, the association is always registered.")]
    public List<string>? Features { get; set; }
}
