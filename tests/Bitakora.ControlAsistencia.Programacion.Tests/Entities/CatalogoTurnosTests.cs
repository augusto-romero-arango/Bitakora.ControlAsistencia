// Issue #288 CA-3: coherencia entre CatalogoTurnos.ObtenerDetalle().Descripcion y
// CatalogoTurnos.ToString(). El formato de nivel turno ("{nombre} {franjas}") vive en este
// aggregate, en el Function App de Programacion (el worker de proyecciones no puede referenciarlo,
// MEF-ADR-0034 seccion 5). ObtenerDetalle() e Iniciar() son internal (ADR-0015); accesibles en este
// proyecto de tests via InternalsVisibleTo (Bitakora.ControlAsistencia.Programacion.csproj).
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class CatalogoTurnosTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000099");

    private static CatalogoTurnos CrearCatalogo(params DatosFranja[] franjas) =>
        CatalogoTurnos.Iniciar(TurnoCreado.Crear(TurnoId, "Turno Manana", franjas));

    private static DatosFranja Ordinaria(
        TimeOnly inicio, TimeOnly fin, List<(TimeOnly inicio, TimeOnly fin)>? descansos = null) =>
        new(inicio, fin, descansos ?? [], []);

    [Fact]
    public void ObtenerDetalle_TieneDescripcionCoherenteConToString_CuandoTurnoConUnaOrdinaria()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        var detalle = catalogo.ObtenerDetalle();

        detalle.Descripcion.Should().Be(catalogo.ToString());
    }

    // El ToString() del turno concatena las ordinarias y arrastra los labels .resx de los
    // descansos (MEF-ADR-0009): el caso compuesto es el que puede divergir, no el simple.
    [Fact]
    public void ObtenerDetalle_TieneDescripcionCoherenteConToString_CuandoTurnoPartidoConDescanso()
    {
        var catalogo = CrearCatalogo(
            Ordinaria(new TimeOnly(6, 0), new TimeOnly(12, 0),
                [(new TimeOnly(9, 0), new TimeOnly(9, 15))]),
            Ordinaria(new TimeOnly(14, 0), new TimeOnly(18, 0)));

        var detalle = catalogo.ObtenerDetalle();

        detalle.Descripcion.Should().Be(catalogo.ToString());
        detalle.FranjasOrdinarias.Should().HaveCount(2);
        detalle.FranjasOrdinarias[0].Descansos[0].Descripcion.Should().Be("(09:00-09:15)");
    }

    // ---------- Issue #335 CA-1/CA-2: sede prearmada por franja en el catalogo ----------

    // CA-1: turno partido con sede diferente en cada franja (ej. "Vigilante partido": manana ->
    // Suba, tarde -> Chapinero) -- el detalle del catalogo expone la sede de cada franja.
    [Fact]
    public void ObtenerDetalle_ExponeSedePorFranja_CuandoTurnoPrearmaSedesDiferentesEnCadaFranja()
    {
        var sedeManana = new SedeProgramada("SEDE-SUBA", "Suba");
        var sedeTarde = new SedeProgramada("SEDE-CHAPINERO", "Chapinero");
        var catalogo = CrearCatalogo(
            new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], sedeManana),
            new DatosFranja(new TimeOnly(14, 0), new TimeOnly(22, 0), [], [], sedeTarde));

        var detalle = catalogo.ObtenerDetalle();

        detalle.FranjasOrdinarias[0].Sede.Should().Be(sedeManana);
        detalle.FranjasOrdinarias[1].Sede.Should().Be(sedeTarde);
    }

    // CA-2: regresion -- un turno sin sedes prearmadas conserva el comportamiento actual.
    [Fact]
    public void ObtenerDetalle_DejaSedeNull_CuandoTurnoNoPrearmaNingunaSede()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        var detalle = catalogo.ObtenerDetalle();

        detalle.FranjasOrdinarias[0].Sede.Should().BeNull();
    }

    // ---------- CA-4: EstaCompleto() y las tres formas de ToString() ----------

    private static CatalogoTurnos CrearCatalogoDescanso(string nombre) =>
        CatalogoTurnos.Iniciar(TurnoCreado.CrearDescanso(TurnoId, nombre));

    [Fact]
    public void EstaCompleto_EsTrue_CuandoTurnoEsDescanso()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");

        catalogo.EstaCompleto().Should().BeTrue();
        catalogo.ToString().Should().Be($"Descanso Compensatorio {CatalogoTurnos.Mensajes.LabelDescanso}");
    }

    [Fact]
    public void EstaCompleto_EsFalse_CuandoTurnoNaceVacioSinMarcaDeDescanso()
    {
        var catalogo = CrearCatalogo();

        catalogo.EstaCompleto().Should().BeFalse();
        catalogo.ToString().Should().Be($"Turno Manana {CatalogoTurnos.Mensajes.LabelIncompleto}");
    }

    [Fact]
    public void EstaCompleto_EsTrue_CuandoTurnoTieneAlMenosUnaFranja()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        catalogo.EstaCompleto().Should().BeTrue();
        catalogo.ToString().Should().Be(catalogo.ObtenerDetalle().Descripcion);
    }
}
