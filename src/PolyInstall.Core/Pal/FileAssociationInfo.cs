namespace PolyInstall.Pal;

public class FileAssociationInfo
{
    public string Extension { get; set; } = "";
    public string Description { get; set; } = "";
    public string ProgId { get; set; } = "";
    public string? Icon { get; set; }
    public string Command { get; set; } = "";
}
