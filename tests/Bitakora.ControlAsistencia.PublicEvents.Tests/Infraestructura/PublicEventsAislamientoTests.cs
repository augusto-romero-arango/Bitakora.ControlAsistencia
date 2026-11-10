// PublicEvents esta sin tipos: estos tests custodian las dos condiciones estructurales que lo hacen
// la isla del Published Language (CA-ADR-0029 decision #2) mientras ningun tipo las sostiene por
// compilacion. Se lee el .csproj y no el assembly compilado: es la unica forma de detectar una
// referencia declarada que ningun tipo usa.

using System.Runtime.CompilerServices;
using AwesomeAssertions;

namespace Bitakora.ControlAsistencia.PublicEvents.Tests.Infraestructura;

public class PublicEventsAislamientoTests
{
    private static string RutaCsproj([CallerFilePath] string? archivoDeEsteTest = null)
    {
        var directorioDeEsteTest = Path.GetDirectoryName(archivoDeEsteTest)!;
        var raizRepo = Path.GetFullPath(Path.Combine(directorioDeEsteTest, "..", "..", ".."));
        return Path.Combine(
            raizRepo,
            "src",
            "Bitakora.ControlAsistencia.PublicEvents",
            "Bitakora.ControlAsistencia.PublicEvents.csproj");
    }

    // Cero ProjectReference es la condicion para empaquetar este ensamblado como NuGet sin
    // arrastrar tipos internos del bounded context al consumidor externo.
    [Fact]
    public void Csproj_NoTieneProjectReference()
    {
        var contenido = File.ReadAllText(RutaCsproj());

        contenido.Should().NotContain("<ProjectReference");
    }

    // El paquete que expone IPublicEvent distingue a esta isla de un {Dominio}.DomainEvents (que no
    // debe tenerlo). Sin tipos que lo usen, una limpieza de "dependencias sin uso" lo retiraria y el
    // proximo evento publico naceria sin marker de bus.
    [Fact]
    public void Csproj_ConservaElPaqueteQueExponeElMarkerDeBusPublico()
    {
        var contenido = File.ReadAllText(RutaCsproj());

        contenido.Should().Contain("Cosmos.EventDriven.Abstractions");
    }
}
