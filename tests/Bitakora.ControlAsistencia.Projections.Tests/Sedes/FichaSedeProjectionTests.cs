// Invocacion directa de los metodos estaticos de FichaSedeProjection, sin el DSL de
// CommandHandlerTestBase. Oraculos armados a mano (MEF-ADR-0002): la vistaPrevia de los tests de
// Apply se construye a mano, nunca invocando Create/Apply.

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

    // La identidad del documento es el StreamKey del stream ("s:001"), nunca recomputada desde el
    // payload.
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

    [Fact]
    public void Apply_ReemplazaElNombre_CuandoNombreSedeModificado()
    {
        var vistaPrevia = FichaDePrueba();
        var evento = new NombreSedeModificado("Sede Centro Renovada");

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { Nombre = "Sede Centro Renovada" });
    }

    // UbicacionActualizada reemplaza ambas, aun hacia null: nunca hace merge parcial.
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

    [Fact]
    public void Apply_AsignaElCentroDeCostosVigente_CuandoCentroDeCostosAsignado()
    {
        var vistaPrevia = FichaDePrueba();
        var evento = new CentroDeCostosAsignado("CC-100");

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { CentroDeCostos = "CC-100" });
    }

    [Fact]
    public void Apply_RetiraElCentroDeCostosVigente_CuandoCentroDeCostosRetirado()
    {
        var vistaPrevia = FichaDePrueba(centroDeCostos: "CC-100");
        var evento = new CentroDeCostosRetirado();

        var vista = FichaSedeProjection.Apply(evento, vistaPrevia);

        vista.Should().Be(vistaPrevia with { CentroDeCostos = null });
    }

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

    // Listas previas no vacias: descarta una implementacion que sobrescriba todo el listado.
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
