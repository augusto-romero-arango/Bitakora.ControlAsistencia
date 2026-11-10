using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;

/// <summary>
/// La construccion de VOs se delega a FranjaOrdinaria.Crear() -- sus errores se acumulan. La
/// traduccion desde el comando CrearTurno la prueba CrearTurnoCommandHandlerTests.
/// </summary>
public class TurnoCreadoTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000001");
    private const string NombreValido = "Turno Manana";

    private static DatosFranja FranjaDiurnaSimple() =>
        new(new TimeOnly(8, 0), new TimeOnly(16, 0), [], []);

    [Fact]
    public void Crear_RetornaTurnoCreado_CuandoUnaOrdinariaSimpleSinHijos()
    {
        var evento = TurnoCreado.Crear(TurnoId, NombreValido, [FranjaDiurnaSimple()]);

        evento.TurnoId.Should().Be(TurnoId);
        evento.Nombre.Should().Be(NombreValido);
        evento.FranjasOrdinarias.Should().HaveCount(1);
        evento.FranjasOrdinarias[0].ToString().Should().Be("08:00-16:00");
    }

    [Fact]
    public void Crear_RetornaTurnoCreadoConDosOrdinarias_CuandoJornadaPartida()
    {
        var evento = TurnoCreado.Crear(
            TurnoId,
            "Turno Partido",
            [
                new DatosFranja(new TimeOnly(6, 0), new TimeOnly(12, 0), [], []),
                new DatosFranja(new TimeOnly(14, 0), new TimeOnly(16, 0), [], [])
            ]);

        evento.FranjasOrdinarias.Should().HaveCount(2);
        evento.FranjasOrdinarias[0].ToString().Should().Be("06:00-12:00");
        evento.FranjasOrdinarias[1].ToString().Should().Be("14:00-16:00");
    }

    [Fact]
    public void Crear_RetornaTurnoCreadoConDescansosYExtras_CuandoOrdinariaConHijos()
    {
        var descanso = (new TimeOnly(10, 0), new TimeOnly(10, 15));
        var extra = (new TimeOnly(6, 0), new TimeOnly(8, 0));

        var evento = TurnoCreado.Crear(
            TurnoId,
            NombreValido,
            [new DatosFranja(
                new TimeOnly(6, 0), new TimeOnly(12, 0),
                [descanso], [extra])]);

        evento.FranjasOrdinarias.Should().HaveCount(1);
        evento.FranjasOrdinarias[0].ToString()
            .Should().Be("06:00-12:00, descanso 10:00-10:15, extra 06:00-08:00");
    }

    [Fact]
    public void Crear_RetornaTurnoCreadoNocturno_CuandoOrdinariaCruzaMedianoche()
    {
        var evento = TurnoCreado.Crear(
            TurnoId,
            "Turno Nocturno",
            [new DatosFranja(new TimeOnly(22, 0), new TimeOnly(6, 0), [], [])]);

        evento.FranjasOrdinarias.Should().HaveCount(1);
        evento.FranjasOrdinarias[0].ToString().Should().Be("22:00-06:00+1");
    }

    [Fact]
    public void Crear_RetornaTurnoCreadoNocturnoConDescanso_CuandoDescansoEstaContenidoEnOrdinariaNocturna()
    {
        // Descanso de 23:00 a 23:15 esta contenido dentro de la franja 22:00-06:00+1
        var descanso = (new TimeOnly(23, 0), new TimeOnly(23, 15));

        var evento = TurnoCreado.Crear(
            TurnoId,
            "Turno Nocturno",
            [new DatosFranja(
                new TimeOnly(22, 0), new TimeOnly(6, 0),
                [descanso], [])]);

        evento.FranjasOrdinarias[0].ToString()
            .Should().Be("22:00-06:00+1, descanso 23:00-23:15");
    }

    [Fact]
    public void Crear_RetornaTurnoCreadoConFranjasVaciasYEsDescansoFalso_CuandoListaDeOrdinariasEstaVacia()
    {
        var evento = TurnoCreado.Crear(TurnoId, NombreValido, []);

        evento.TurnoId.Should().Be(TurnoId);
        evento.Nombre.Should().Be(NombreValido);
        evento.FranjasOrdinarias.Should().BeEmpty();
        evento.EsDescanso.Should().BeFalse();
    }

    [Fact]
    public void Crear_TieneEsDescansoFalso_CuandoHayAlMenosUnaFranja()
    {
        var evento = TurnoCreado.Crear(TurnoId, NombreValido, [FranjaDiurnaSimple()]);

        evento.EsDescanso.Should().BeFalse();
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoNombreEstaVacio()
    {
        var act = () => TurnoCreado.Crear(TurnoId, "", [FranjaDiurnaSimple()]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.NombreVacio));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoNombreEsSoloEspaciosEnBlanco()
    {
        var act = () => TurnoCreado.Crear(TurnoId, "   ", [FranjaDiurnaSimple()]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.NombreVacio));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoFranjasOrdinariasSeSolapan()
    {
        // 6:00-12:00 se solapa con 10:00-16:00
        var act = () => TurnoCreado.Crear(
            TurnoId,
            NombreValido,
            [
                new DatosFranja(new TimeOnly(6, 0), new TimeOnly(12, 0), [], []),
                new DatosFranja(new TimeOnly(10, 0), new TimeOnly(16, 0), [], [])
            ]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.FranjasOrdinariasSeSolapan));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoFranjasNocturnasSeSolapan()
    {
        // 22:00-06:00+1 se solapa con 23:00-07:00+1 (ambas cruzan medianoche)
        var act = () => TurnoCreado.Crear(
            TurnoId,
            NombreValido,
            [
                new DatosFranja(new TimeOnly(22, 0), new TimeOnly(6, 0), [], []),
                new DatosFranja(new TimeOnly(23, 0), new TimeOnly(7, 0), [], [])
            ]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.FranjasOrdinariasSeSolapan));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoSubFranjaEstaFueraDelContenedor()
    {
        // Descanso de 14:00 a 15:00 esta fuera de la franja 8:00-12:00
        var descansoFuera = (new TimeOnly(14, 0), new TimeOnly(15, 0));

        var act = () => TurnoCreado.Crear(
            TurnoId,
            NombreValido,
            [new DatosFranja(
                new TimeOnly(8, 0), new TimeOnly(12, 0),
                [descansoFuera], [])]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae =>
                ae.Message.Contains(FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor));
    }

    [Fact]
    public void Crear_LanzaAggregateExceptionConUnSoloError_CuandoHayNombreVacioYSinOrdinarias()
    {
        // La ausencia de franjas no es invalida: solo queda el error de nombre.
        var act = () => TurnoCreado.Crear(TurnoId, "", []);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().HaveCount(1);
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.NombreVacio));
    }

    [Fact]
    public void Crear_LanzaAggregateExceptionConErroresPropiosYDelegados_CuandoNombreVacioYSubFranjaInvalida()
    {
        // Nombre vacio (error propio) + descanso fuera del contenedor (error delegado de VO)
        var descansoFuera = (new TimeOnly(14, 0), new TimeOnly(15, 0));

        var act = () => TurnoCreado.Crear(
            TurnoId,
            "",
            [new DatosFranja(
                new TimeOnly(8, 0), new TimeOnly(12, 0),
                [descansoFuera], [])]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().HaveCount(2);
        ex.InnerExceptions.Should().Contain(e => e.Message.Contains(TurnoCreado.Mensajes.NombreVacio));
        ex.InnerExceptions.Should().Contain(e =>
            e.Message.Contains(FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor));
    }

    [Fact]
    public void Crear_SoloLanzaArgumentExceptions_CuandoHayErroresDeValidacion()
    {
        var act = () => TurnoCreado.Crear(TurnoId, "", []);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().AllBeAssignableTo<ArgumentException>();
    }

    [Fact]
    public void Crear_PropagaSedePorFranja_CuandoDatosFranjaTraeSedeValida()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");

        var evento = TurnoCreado.Crear(
            TurnoId, NombreValido,
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], sede)]);

        evento.FranjasOrdinarias[0].ToDetalle().Sede.Should().Be(sede);
    }

    // Sede con Id vacio se acumula junto a las demas invariantes del factory.
    [Fact]
    public void Crear_LanzaAggregateException_CuandoSedeDeFranjaTieneIdVacio()
    {
        var sedeInvalida = new SedeProgramada("", "Suba");

        var act = () => TurnoCreado.Crear(
            TurnoId, NombreValido,
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], sedeInvalida)]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(FranjaOrdinaria.Mensajes.SedeIncompleta));
    }

    // Sede invalida se acumula junto a otros errores (no fail-fast).
    [Fact]
    public void Crear_LanzaAggregateExceptionConErroresPropiosYDeSede_CuandoNombreVacioYSedeIncompleta()
    {
        var sedeInvalida = new SedeProgramada("SEDE-SUBA", "   ");

        var act = () => TurnoCreado.Crear(
            TurnoId, "",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], sedeInvalida)]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().HaveCount(2);
        ex.InnerExceptions.Should().Contain(e => e.Message.Contains(TurnoCreado.Mensajes.NombreVacio));
        ex.InnerExceptions.Should().Contain(e => e.Message.Contains(FranjaOrdinaria.Mensajes.SedeIncompleta));
    }

    [Fact]
    public void CrearDescanso_RetornaTurnoCreadoConFranjasVacias_CuandoNombreValido()
    {
        var evento = TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

        evento.TurnoId.Should().Be(TurnoId);
        evento.Nombre.Should().Be("Descanso Compensatorio");
        evento.FranjasOrdinarias.Should().BeEmpty();
    }

    [Fact]
    public void CrearDescanso_TieneEsDescansoTrue_CuandoNombreValido()
    {
        var evento = TurnoCreado.CrearDescanso(TurnoId, "Descanso Compensatorio");

        evento.EsDescanso.Should().BeTrue();
    }

    [Fact]
    public void CrearDescanso_LanzaAggregateException_CuandoNombreEstaVacio()
    {
        var act = () => TurnoCreado.CrearDescanso(TurnoId, "");

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.NombreVacio));
    }

    [Fact]
    public void CrearDescanso_LanzaAggregateException_CuandoNombreEsSoloEspaciosEnBlanco()
    {
        var act = () => TurnoCreado.CrearDescanso(TurnoId, "   ");

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.NombreVacio));
    }

    // Con offsets inferidos, el unico par (inicio, fin) que ninguna inferencia rescata es uno
    // identico: offsetFin solo supera a offsetInicio cuando fin es menor que inicio.
    [Fact]
    public void Crear_LanzaAggregateException_CuandoDescansoTieneDuracionNoPositiva()
    {
        var descansoInvalido = (new TimeOnly(23, 30), new TimeOnly(23, 30));

        var act = () => TurnoCreado.Crear(
            TurnoId,
            NombreValido,
            [new DatosFranja(
                new TimeOnly(22, 0), new TimeOnly(6, 0),
                [descansoInvalido], [])]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(FranjaTemporal.Mensajes.DuracionNoPositiva));
    }

    [Fact]
    public void Crear_LanzaAggregateExceptionConDosErrores_CuandoNombreVacioYDescansoTieneDuracionNoPositiva()
    {
        var descansoInvalido = (new TimeOnly(23, 30), new TimeOnly(23, 30));

        var act = () => TurnoCreado.Crear(
            TurnoId,
            "",
            [new DatosFranja(
                new TimeOnly(22, 0), new TimeOnly(6, 0),
                [descansoInvalido], [])]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().HaveCount(2);
        ex.InnerExceptions.Should().Contain(e => e.Message.Contains(TurnoCreado.Mensajes.NombreVacio));
        ex.InnerExceptions.Should().Contain(e => e.Message.Contains(FranjaTemporal.Mensajes.DuracionNoPositiva));
    }

    // Los offsets de las hijas se infieren relativos a su franja contenedora.
    [Fact]
    public void Crear_InfiereOffsetsDeHijas_CuandoDescansoYExtraCaenEnLaMadrugada()
    {
        var descansoMadrugada = (new TimeOnly(2, 0), new TimeOnly(2, 30));
        var extraMadrugada = (new TimeOnly(5, 0), new TimeOnly(5, 30));

        var evento = TurnoCreado.Crear(
            TurnoId,
            NombreValido,
            [new DatosFranja(
                new TimeOnly(22, 0), new TimeOnly(6, 0),
                [descansoMadrugada], [extraMadrugada])]);

        evento.FranjasOrdinarias[0].ToString().Should().Be(
            "22:00-06:00+1, descanso 02:00+1-02:30+1, extra 05:00+1-05:30+1");
    }

    [Fact]
    public void Crear_RetornaFranjaDe24Horas_CuandoDiaOffsetFinEsExplicito()
    {
        var evento = TurnoCreado.Crear(
            TurnoId, NombreValido,
            [new DatosFranja(new TimeOnly(8, 0), new TimeOnly(8, 0), [], [], DiaOffsetFin: 1)]);

        evento.FranjasOrdinarias[0].ToString().Should().Be("08:00-08:00+1");
        evento.FranjasOrdinarias[0].DuracionEnMinutos().Should().Be(1440);
    }

    // El solape entre ordinarias debe usar el offset EXPLICITO, no la inferencia por defecto
    // (fin < inicio): con inicio == fin, la inferencia daria offset 0 y una franja de 24 h se
    // veria de duracion cero para este chequeo, dejando pasar una segunda ordinaria contenida.
    [Fact]
    public void Crear_LanzaAggregateException_CuandoSegundaOrdinariaSeSolapaConFranjaDe24HorasExplicita()
    {
        var act = () => TurnoCreado.Crear(
            TurnoId, NombreValido,
            [
                new DatosFranja(new TimeOnly(8, 0), new TimeOnly(8, 0), [], [], DiaOffsetFin: 1),
                new DatosFranja(new TimeOnly(10, 0), new TimeOnly(12, 0), [], [])
            ]);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.FranjasOrdinariasSeSolapan));
    }

    [Fact]
    public void Crear_InfiereOffsetMasUno_CuandoDiaOffsetFinEsCeroYFinEsMenorQueInicio()
    {
        var evento = TurnoCreado.Crear(
            TurnoId, NombreValido,
            [new DatosFranja(new TimeOnly(22, 0), new TimeOnly(6, 0), [], [], DiaOffsetFin: 0)]);

        evento.FranjasOrdinarias[0].ToString().Should().Be("22:00-06:00+1");
    }
}
