// Issue #461: primera proyeccion concreta del dominio Sedes. Invocacion DIRECTA de los metodos
// estaticos de FichaSedeProjection (N1, MEF-ADR-0035) -- no el DSL Given/When/Then de
// CommandHandlerTestBase (MEF-ADR-0002, testea command handlers contra el event store): aqui se
// testean funciones puras evento -> vista, sin abrir ningun stream.
//
// Cada assert compara contra un oraculo armado a mano (MEF-ADR-0002, no-tautologia): nunca se reusa
// la logica de Create/Apply bajo prueba para construir el valor esperado.
//
// CA-1..CA-4 son variaciones del mismo eje (materializacion Create/Apply); CA-5/CA-6 (los
// endpoints) no se prueban aqui -- ver el test de composicion en Sedes.Tests.
//
// Dato de vistaPrevia en los tests de Apply: se construye a mano, simulando el documento que Marten
// ya habria materializado -- nunca se obtiene invocando Create/Apply, para no encadenar el oraculo
// de un test con la logica bajo prueba de otro.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Projections.Sedes;
using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Sedes;

public class FichaSedeProjectionTests
{
    private static FichaSede FichaDePrueba(
        string centroDeCostos = null!, bool activa = true, string[]? dispositivos = null) =>
        new("s:001", "001", "Sede Centro", "Bogota", "Calle 1", centroDeCostos, activa, dispositivos ?? []);

    // --- CA-1: Create nace la ficha Activa=true, sin CC ni dispositivos ---

    // Create toma IEvent<SedeRegistrada>, no el evento a secas: la identidad del documento es el
    // StreamKey del stream de SedeAggregateRoot ("s:001"), nunca recomputada a mano desde el
    // payload (skills/projections/modelos-marten.md).
    [Fact]
    public void Create_ProyectaSedeActivaSinCentroDeCostosNiDispositivos_DesdeSedeRegistrada()
    {
        var evento = new Event<SedeRegistrada>(new SedeRegistrada("001", "Sede Centro", "Bogota", "Calle 1"))
        {
            StreamKey = "s:001",
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = FichaSedeProjection.Create(evento);

        vista.Should().Be(new FichaSede("s:001", "001", "Sede Centro", "Bogota", "Calle 1", null, true, []));
    }

    // --- CA-2 (primera mitad): NombreSedeModificado reemplaza Nombre ---

    [Fact]
    public void Apply_ReemplazaElNombre_CuandoNombreSedeModificado()
    {
        var vistaPrevia = FichaDePrueba();
        var evento = new NombreSedeModificado("Sede Centro Renovada");

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { Nombre = "Sede Centro Renovada" });
    }

    // --- CA-2 (segunda mitad): UbicacionActualizada reemplaza Ciudad+Direccion ATOMICAMENTE, aun
    // hacia null (el evento reemplaza ambas, nunca hace merge parcial) ---

    [Fact]
    public void Apply_ReemplazaCiudadYDireccionAunHaciaNull_CuandoUbicacionActualizada()
    {
        var vistaPrevia = FichaDePrueba();
        var evento = new UbicacionActualizada(null, null);

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { Ciudad = null, Direccion = null });
    }

    [Fact]
    public void Apply_ReemplazaCiudadYDireccionConNuevosValores_CuandoUbicacionActualizada()
    {
        var vistaPrevia = FichaDePrueba();
        var evento = new UbicacionActualizada("Medellin", "Carrera 50");

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { Ciudad = "Medellin", Direccion = "Carrera 50" });
    }

    // --- CA-3 (primera mitad): CentroDeCostosAsignado refleja el CC vigente ---

    [Fact]
    public void Apply_AsignaElCentroDeCostosVigente_CuandoCentroDeCostosAsignado()
    {
        var vistaPrevia = FichaDePrueba();
        var evento = new CentroDeCostosAsignado("CC-100");

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { CentroDeCostos = "CC-100" });
    }

    // --- CA-3 (segunda mitad): CentroDeCostosRetirado deja el CC en null ---

    [Fact]
    public void Apply_RetiraElCentroDeCostosVigente_CuandoCentroDeCostosRetirado()
    {
        var vistaPrevia = FichaDePrueba(centroDeCostos: "CC-100");
        var evento = new CentroDeCostosRetirado();

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { CentroDeCostos = null });
    }

    // --- CA-4 (primera mitad): SedeActivada/SedeDesactivada conmutan Activa ---

    [Fact]
    public void Apply_ActivaLaSede_CuandoSedeActivada()
    {
        var vistaPrevia = FichaDePrueba(activa: false);
        var evento = new SedeActivada();

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { Activa = true });
    }

    [Fact]
    public void Apply_DesactivaLaSede_CuandoSedeDesactivada()
    {
        var vistaPrevia = FichaDePrueba(activa: true);
        var evento = new SedeDesactivada();

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { Activa = false });
    }

    // --- CA-4 (segunda mitad): DispositivoInstalado/DispositivoRetirado mantienen la lista vigente
    // -- oraculo mas fuerte que "agregar/quitar sobre lista vacia": descarta una implementacion que
    // sobrescriba todo el listado en vez de agregar/quitar ---

    [Fact]
    public void Apply_AgregaElDispositivoInstalado_ManteniendoLosExistentes()
    {
        var vistaPrevia = FichaDePrueba(dispositivos: ["disp-01"]);
        var evento = new DispositivoInstalado("disp-02");

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Dispositivos.Should().BeEquivalentTo(["disp-01", "disp-02"]);
    }

    [Fact]
    public void Apply_RemueveSoloElDispositivoRetirado_DejandoIntactosLosDemas()
    {
        var vistaPrevia = FichaDePrueba(dispositivos: ["disp-01", "disp-02"]);
        var evento = new DispositivoRetirado("disp-01");

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Dispositivos.Should().BeEquivalentTo(["disp-02"]);
    }
}
