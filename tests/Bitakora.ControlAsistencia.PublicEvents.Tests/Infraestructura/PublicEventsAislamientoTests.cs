// Issue #436: PublicEvents quedo SIN TIPOS -- InformacionColaborador, su ultimo habitante, murio al
// reducirse el body de SolicitarProgramacionTurno a la terna de identidad. Estos son los unicos
// tests que le quedan al proyecto, y no es un relleno para evitar el ZeroTestsRan de
// Microsoft.Testing.Platform: custodian las dos condiciones estructurales que hacen de este
// ensamblado la isla del Published Language (CA-ADR-0029 decision #2 / MEF-ADR-0039 decision 2),
// justo mientras esta vacio y ningun tipo las sostiene por compilacion.
//
// Verificado por lectura directa del .csproj (no por reflexion del assembly compilado): es la
// unica forma de detectar una referencia declarada pero no usada por ningun tipo -- y hoy ningun
// tipo usa nada. Mismo patron que ControlHorasDomainEventsAislamientoTests (#322).

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

    // El paquete que expone IPublicEvent es lo que distingue a esta isla de un {Dominio}.DomainEvents
    // (que debe NO tenerlo, #322). Con el ensamblado vacio ningun tipo lo usa, asi que una limpieza
    // de "dependencias sin uso" lo retiraria y el proximo evento publico naceria sin marker de bus.
    [Fact]
    public void Csproj_ConservaElPaqueteQueExponeElMarkerDeBusPublico()
    {
        var contenido = File.ReadAllText(RutaCsproj());

        contenido.Should().Contain("Cosmos.EventDriven.Abstractions");
    }
}
