using PolyInstall.Install;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Tests;

public class ProductIdHelperTests
{
    [Fact]
    public void StableProductGuidString_WithIdenticalMetadata_ReturnsSameValue()
    {
        var m = new ManifestMetadata { Name = "App", Version = "1.0", Publisher = "Contoso" };
        var a = ProductIdHelper.StableProductGuidString(m);
        var b = ProductIdHelper.StableProductGuidString(m);
        a.Verify().ToBe(b);
        a.Verify().ToStartWith("{");
        a.Verify().ToEndWith("}");
    }

    [Fact]
    public void StableProductGuidString_WithGuidId_ReturnsUpperInvariantGuid()
    {
        var id = "{11111111-1111-1111-1111-111111111111}";
        var m = new ManifestMetadata { Id = id, Name = "X", Version = "1.0" };
        ProductIdHelper.StableProductGuidString(m).Verify().ToBe(id.ToUpperInvariant());
    }

    [Fact]
    public void StableProductGuidString_WithNonGuidId_ReturnsDeterministicGuid()
    {
        var m = new ManifestMetadata { Id = "contoso-product-key", Name = "X", Version = "1.0" };
        var a = ProductIdHelper.StableProductGuidString(m);
        var b = ProductIdHelper.StableProductGuidString(m);
        a.Verify().ToBe(b);
        Guid.TryParse(a.Trim('{', '}'), out _).Verify().ToBeTrue();
    }
}
