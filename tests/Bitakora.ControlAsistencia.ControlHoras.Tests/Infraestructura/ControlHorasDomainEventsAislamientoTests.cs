// ControlHoras.DomainEvents es una de las "tres islas" (CA-ADR-0029 decision #2): cero
// ProjectReference y cero PackageReference, para que un evento persistido no pueda reintroducir un
// marker de bus (IPublicEvent/IPrivateEvent). Se lee el .csproj y no el assembly compilado: una
// referencia declarada pero no usada no la delata el compilador.

using System.Runtime.CompilerServices;
using AwesomeAssertions;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class ControlHorasDomainEventsAislamientoTests
{
    private static string RutaCsproj([CallerFilePath] string? archivoDeEsteTest = null)
    {
        var directorioDeEsteTest = Path.GetDirectoryName(archivoDeEsteTest)!;
        var raizRepo = Path.GetFullPath(Path.Combine(directorioDeEsteTest, "..", "..", ".."));
        return Path.Combine(
            raizRepo,
            "src",
            "Bitakora.ControlAsistencia.ControlHoras.DomainEvents",
            "Bitakora.ControlAsistencia.ControlHoras.DomainEvents.csproj");
    }

    [Fact]
    public void Csproj_NoTieneProjectReference()
    {
        var contenido = File.ReadAllText(RutaCsproj());

        contenido.Should().NotContain("<ProjectReference");
    }

    [Fact]
    public void Csproj_NoTienePackageReference()
    {
        var contenido = File.ReadAllText(RutaCsproj());

        contenido.Should().NotContain("<PackageReference");
    }
}
