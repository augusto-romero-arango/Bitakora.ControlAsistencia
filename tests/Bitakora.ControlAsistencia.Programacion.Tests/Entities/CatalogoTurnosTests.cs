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
        catalogo.ToString().Should().Be("Turno Manana (06:00-14:00)");
    }

    // ---------- Issue #613 CA-4: EvaluarAsignabilidad() y su precedencia ----------

    [Fact]
    public void EvaluarAsignabilidad_EsRetirado_CuandoElTurnoFueRetirado()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));
        catalogo.Retirar();

        catalogo.EvaluarAsignabilidad().Should().Be(ResultadoAsignabilidadTurno.Retirado);
    }

    [Fact]
    public void EvaluarAsignabilidad_EsIncompleto_CuandoElTurnoNaceVacioSinMarcaDeDescanso()
    {
        var catalogo = CrearCatalogo();

        catalogo.EvaluarAsignabilidad().Should().Be(ResultadoAsignabilidadTurno.Incompleto);
    }

    [Fact]
    public void EvaluarAsignabilidad_EsAsignable_CuandoElTurnoTieneAlMenosUnaFranja()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));

        catalogo.EvaluarAsignabilidad().Should().Be(ResultadoAsignabilidadTurno.Asignable);
    }

    [Fact]
    public void EvaluarAsignabilidad_EsAsignable_CuandoElTurnoEsDescanso()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");

        catalogo.EvaluarAsignabilidad().Should().Be(ResultadoAsignabilidadTurno.Asignable);
    }

    // Precedencia (CA-4): un turno retirado con cero franjas y sin marca de descanso -- por
    // completitud seria Incompleto, pero Retirado gana la precedencia.
    [Fact]
    public void EvaluarAsignabilidad_EsRetirado_CuandoElTurnoEstaRetiradoYAdemasIncompleto()
    {
        var catalogo = CrearCatalogo();
        catalogo.Retirar();

        catalogo.EvaluarAsignabilidad().Should().Be(ResultadoAsignabilidadTurno.Retirado);
    }

    // ---------- Issue #602 CA-2/CA-3/CA-4: AgregarFranja y su precedencia ----------

    // CA-2: el turno pasa de incompleto a completo con la primera franja.
    [Fact]
    public void AgregarFranja_RetornaAgregada_CuandoElTurnoEstaIncompleto()
    {
        var catalogo = CrearCatalogo();
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));

        var resultado = catalogo.AgregarFranja(franja);

        resultado.Should().Be(ResultadoAgregarFranja.Agregada);
        catalogo.UncommittedEvents.OfType<FranjaAgregada>().Should().ContainSingle()
            .Which.Franja.Should().Be(franja);
        catalogo.EstaCompleto().Should().BeTrue();
        catalogo.ToString().Should().Be("Turno Manana (22:00-06:00+1)");
    }

    // CA-3: fin exclusivo -- una franja contigua a la existente no se solapa y se agrega,
    // conservando el orden de insercion en ToString().
    [Fact]
    public void AgregarFranja_RetornaAgregada_CuandoLaNuevaFranjaEsContiguaALaExistente()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));
        var nueva = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

        var resultado = catalogo.AgregarFranja(nueva);

        resultado.Should().Be(ResultadoAgregarFranja.Agregada);
        catalogo.ToString().Should().Be("Turno Manana (06:00-14:00)(14:00-22:00)");
    }

    // CA-3: una franja que se superpone parcialmente con la existente se rechaza sin emitir evento.
    [Fact]
    public void AgregarFranja_RetornaSeSolapaConOtraFranja_CuandoLaNuevaFranjaSeSuperponeConLaExistente()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));
        var nueva = FranjaOrdinaria.Crear(new TimeOnly(10, 0), new TimeOnly(12, 0));

        var resultado = catalogo.AgregarFranja(nueva);

        resultado.Should().Be(ResultadoAgregarFranja.SeSolapaConOtraFranja);
        catalogo.UncommittedEvents.OfType<FranjaAgregada>().Should().BeEmpty();
        catalogo.ObtenerDetalle().FranjasOrdinarias.Should().HaveCount(1);
    }

    // CA-4: un turno de descanso no admite franjas ordinarias.
    [Fact]
    public void AgregarFranja_RetornaTurnoEsDescanso_CuandoElTurnoEsDeDescanso()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));

        var resultado = catalogo.AgregarFranja(franja);

        resultado.Should().Be(ResultadoAgregarFranja.TurnoEsDescanso);
        catalogo.UncommittedEvents.OfType<FranjaAgregada>().Should().BeEmpty();
    }

    // CA-4: un turno retirado no admite nuevas franjas.
    [Fact]
    public void AgregarFranja_RetornaTurnoRetirado_CuandoElTurnoFueRetirado()
    {
        var catalogo = CrearCatalogo(Ordinaria(new TimeOnly(6, 0), new TimeOnly(14, 0)));
        catalogo.Retirar();
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0));

        var resultado = catalogo.AgregarFranja(franja);

        resultado.Should().Be(ResultadoAgregarFranja.TurnoRetirado);
        catalogo.UncommittedEvents.OfType<FranjaAgregada>().Should().BeEmpty();
    }

    // CA-4: precedencia -- un turno de descanso retirado devuelve TurnoRetirado, no TurnoEsDescanso.
    [Fact]
    public void AgregarFranja_RetornaTurnoRetirado_CuandoElTurnoEsDescansoYAdemasFueRetirado()
    {
        var catalogo = CrearCatalogoDescanso("Descanso Compensatorio");
        catalogo.Retirar();
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0));

        var resultado = catalogo.AgregarFranja(franja);

        resultado.Should().Be(ResultadoAgregarFranja.TurnoRetirado);
    }
}
