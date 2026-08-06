// Issue #322: CA-3. ControlHoras.DomainEvents es una de las "tres islas" de ensamblados de
// eventos (CA-ADR-0029 decision #2 / MEF-ADR-0039 decision #2): cero <ProjectReference> (ni hacia
// PublicEvents/PrivateEvents ni hacia ningun otro proyecto del repo) y cero <PackageReference> --
// el retiro de Cosmos.EventDriven.Abstractions cierra la posibilidad de reintroducir un marker de
// bus (IPublicEvent/IPrivateEvent) en un evento persistido por compilacion.
//
// Verificado por lectura directa del .csproj (no por reflexion del assembly compilado): esta es
// la unica forma de detectar una referencia declarada pero no usada por ningun tipo, que el
// compilador no delataria.

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
