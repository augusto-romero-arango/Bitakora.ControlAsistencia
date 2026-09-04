// Issue #3: Implementar evento TurnoCreado con factory de construccion y validacion

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;

/// <summary>
/// Tests del factory TurnoCreado.Crear(Guid, string, IReadOnlyList&lt;DatosFranja&gt;).
/// Interfaz publica: Crear(...), TurnoId, Nombre, FranjasOrdinarias.
/// La construccion de VOs se delega a FranjaOrdinaria.Crear() -- sus errores se acumulan.
/// Issue #237: el factory recibe DatosFranja y ya no el comando CrearTurno, que vive en la
/// Function App. La traduccion desde el comando la prueba CrearTurnoCommandHandlerTests.
/// </summary>
public class TurnoCreadoTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000001");
    private const string NombreValido = "Turno Manana";

    // Factory method de ayuda para una franja diurna simple sin hijos
    private static DatosFranja FranjaDiurnaSimple() =>
        new(new TimeOnly(8, 0), new TimeOnly(16, 0), [], []);

    // ---------- CA-2: una sola ordinaria sin descansos ni extras ----------

    [Fact]
    public void Crear_RetornaTurnoCreado_CuandoUnaOrdinariaSimpleSinHijos()
    {
        var evento = TurnoCreado.Crear(TurnoId, NombreValido, [FranjaDiurnaSimple()]);

        evento.TurnoId.Should().Be(TurnoId);
        evento.Nombre.Should().Be(NombreValido);
        evento.FranjasOrdinarias.Should().HaveCount(1);
        evento.FranjasOrdinarias[0].ToString().Should().Be("(08:00-16:00)");
    }

    // ---------- CA-3: multiples ordinarias (jornada partida) ----------

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
        evento.FranjasOrdinarias[0].ToString().Should().Be("(06:00-12:00)");
        evento.FranjasOrdinarias[1].ToString().Should().Be("(14:00-16:00)");
    }

    // ---------- CA-4: ordinaria con descansos y extras ----------

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
            .Should().Be("(06:00-12:00)[Descansos:(10:00-10:15)][Extras:(06:00-08:00)]");
    }

    // ---------- CA-5: turno nocturno que cruza medianoche ----------

    [Fact]
    public void Crear_RetornaTurnoCreadoNocturno_CuandoOrdinariaCruzaMedianoche()
    {
        var evento = TurnoCreado.Crear(
            TurnoId,
            "Turno Nocturno",
            [new DatosFranja(new TimeOnly(22, 0), new TimeOnly(6, 0), [], [])]);

        evento.FranjasOrdinarias.Should().HaveCount(1);
        evento.FranjasOrdinarias[0].ToString().Should().Be("(22:00-06:00+1)");
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
            .Should().Be("(22:00-06:00+1)[Descansos:(23:00-23:15)]");
    }

    // ---------- CA-6: sin franjas ordinarias ----------

    [Fact]
    public void Crear_LanzaAggregateException_CuandoListaDeOrdinariasEstaVacia()
    {
        var act = () => TurnoCreado.Crear(TurnoId, NombreValido, []);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.SinFranjasOrdinarias));
    }

    // ---------- CA-7: nombre vacio o solo espacios ----------

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

    // ---------- CA-8: solapamiento entre ordinarias ----------

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

    // ---------- CA-9: errores de FranjaOrdinaria.Crear() se capturan y acumulan ----------

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

    // ---------- CA-10: acumulacion de multiples errores sin fail-fast ----------

    [Fact]
    public void Crear_LanzaAggregateExceptionConTodosLosErrores_CuandoHayNombreVacioYSinOrdinarias()
    {
        // Nombre vacio + sin ordinarias = exactamente 2 errores
        var act = () => TurnoCreado.Crear(TurnoId, "", []);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().HaveCount(2);
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

    // ---------- CA-11: cada error individual es ArgumentException ----------

    [Fact]
    public void Crear_SoloLanzaArgumentExceptions_CuandoHayErroresDeValidacion()
    {
        var act = () => TurnoCreado.Crear(TurnoId, "", []);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().AllBeAssignableTo<ArgumentException>();
    }

    // ---------- Issue #335: sede prearmada por franja ----------

    // CA-1: la sede prearmada de una franja fluye hasta el evento persistido.
    [Fact]
    public void Crear_PropagaSedePorFranja_CuandoDatosFranjaTraeSedeValida()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");

        var evento = TurnoCreado.Crear(
            TurnoId, NombreValido,
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], sede)]);

        evento.FranjasOrdinarias[0].ToDetalle().Sede.Should().Be(sede);
    }

    // CA-3: sede con Id vacio se acumula junto a las demas invariantes del factory.
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

    // CA-3: sede invalida se acumula junto a otros errores (no fail-fast).
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

    // ---------- Issue #598 CA-7: descanso con duracion no positiva se acumula como error delegado ----------

    [Fact]
    public void Crear_LanzaAggregateException_CuandoDescansoTieneDuracionNoPositiva()
    {
        // Ordinaria nocturna 22:00-06:00 con descanso (23:30, 00:30) sin offsets explicitos:
        // SubFranja.Crear() infiere offsets 0/0 y hoy construye -1380 min (defecto que este issue cierra).
        var descansoInvalido = (new TimeOnly(23, 30), new TimeOnly(0, 30));

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
        var descansoInvalido = (new TimeOnly(23, 30), new TimeOnly(0, 30));

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
}
