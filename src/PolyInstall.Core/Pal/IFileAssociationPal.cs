namespace PolyInstall.Pal;

public interface IFileAssociationPal
{
    void Register(FileAssociationInfo association);
    void Unregister(FileAssociationInfo association);
}
