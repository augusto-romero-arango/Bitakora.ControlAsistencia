// Issue #3: Implementar evento TurnoCreado con factory de construccion y validacion

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Eventos;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;

/// <summary>
/// Tests del factory TurnoCreado.Crear(CrearTurno).
/// Interfaz publica: Crear(comando), TurnoId, Nombre, FranjasOrdinarias.
/// La construccion de VOs se delega a FranjaOrdinaria.Crear() -- sus errores se acumulan.
/// </summary>
public class TurnoCreadoTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000001");
    private const string NombreValido = "Turno Manana";

    // Factory method de ayuda para una franja diurna simple sin hijos
    private static CrearTurno.Franja FranjaDiurnaSimple() =>
        new(new TimeOnly(8, 0), new TimeOnly(16, 0), [], []);

    // ---------- CA-2: una sola ordinaria sin descansos ni extras ----------

    [Fact]
    public void Crear_RetornaTurnoCreado_CuandoUnaOrdinariaSimpleSinHijos()
    {
        var comando = new CrearTurno(TurnoId, NombreValido, [FranjaDiurnaSimple()]);

        var evento = TurnoCreado.Crear(comando);

        evento.TurnoId.Should().Be(TurnoId);
        evento.Nombre.Should().Be(NombreValido);
        evento.FranjasOrdinarias.Should().HaveCount(1);
        evento.FranjasOrdinarias[0].ToString().Should().Be("(08:00-16:00)");
    }

    // ---------- CA-3: multiples ordinarias (jornada partida) ----------

    [Fact]
    public void Crear_RetornaTurnoCreadoConDosOrdinarias_CuandoJornadaPartida()
    {
        var comando = new CrearTurno(
            TurnoId,
            "Turno Partido",
            [
                new CrearTurno.Franja(new TimeOnly(6, 0), new TimeOnly(12, 0), [], []),
                new CrearTurno.Franja(new TimeOnly(14, 0), new TimeOnly(16, 0), [], [])
            ]);

        var evento = TurnoCreado.Crear(comando);

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
        var comando = new CrearTurno(
            TurnoId,
            NombreValido,
            [new CrearTurno.Franja(
                new TimeOnly(6, 0), new TimeOnly(12, 0),
                [descanso], [extra])]);

        var evento = TurnoCreado.Crear(comando);

        evento.FranjasOrdinarias.Should().HaveCount(1);
        evento.FranjasOrdinarias[0].ToString()
            .Should().Be("(06:00-12:00)[Descansos:(10:00-10:15)][Extras:(06:00-08:00)]");
    }

    // ---------- CA-5: turno nocturno que cruza medianoche ----------

    [Fact]
    public void Crear_RetornaTurnoCreadoNocturno_CuandoOrdinariaCruzaMedianoche()
    {
        var comando = new CrearTurno(
            TurnoId,
            "Turno Nocturno",
            [new CrearTurno.Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), [], [])]);

        var evento = TurnoCreado.Crear(comando);

        evento.FranjasOrdinarias.Should().HaveCount(1);
        evento.FranjasOrdinarias[0].ToString().Should().Be("(22:00-06:00+1)");
    }

    [Fact]
    public void Crear_RetornaTurnoCreadoNocturnoConDescanso_CuandoDescansoEstaContenidoEnOrdinariaNocturna()
    {
        // Descanso de 23:00 a 23:15 esta contenido dentro de la franja 22:00-06:00+1
        var descanso = (new TimeOnly(23, 0), new TimeOnly(23, 15));
        var comando = new CrearTurno(
            TurnoId,
            "Turno Nocturno",
            [new CrearTurno.Franja(
                new TimeOnly(22, 0), new TimeOnly(6, 0),
                [descanso], [])]);

        var evento = TurnoCreado.Crear(comando);

        evento.FranjasOrdinarias[0].ToString()
            .Should().Be("(22:00-06:00+1)[Descansos:(23:00-23:15)]");
    }

    // ---------- CA-6: sin franjas ordinarias ----------

    [Fact]
    public void Crear_LanzaAggregateException_CuandoListaDeOrdinariasEstaVacia()
    {
        var comando = new CrearTurno(TurnoId, NombreValido, []);

        var act = () => TurnoCreado.Crear(comando);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.SinFranjasOrdinarias));
    }

    // ---------- CA-7: nombre vacio o solo espacios ----------

    [Fact]
    public void Crear_LanzaAggregateException_CuandoNombreEstaVacio()
    {
        var comando = new CrearTurno(TurnoId, "", [FranjaDiurnaSimple()]);

        var act = () => TurnoCreado.Crear(comando);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.NombreVacio));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoNombreEsSoloEspaciosEnBlanco()
    {
        var comando = new CrearTurno(TurnoId, "   ", [FranjaDiurnaSimple()]);

        var act = () => TurnoCreado.Crear(comando);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.NombreVacio));
    }

    // ---------- CA-8: solapamiento entre ordinarias ----------

    [Fact]
    public void Crear_LanzaAggregateException_CuandoFranjasOrdinariasSeSolapan()
    {
        // 6:00-12:00 se solapa con 10:00-16:00
        var comando = new CrearTurno(
            TurnoId,
            NombreValido,
            [
                new CrearTurno.Franja(new TimeOnly(6, 0), new TimeOnly(12, 0), [], []),
                new CrearTurno.Franja(new TimeOnly(10, 0), new TimeOnly(16, 0), [], [])
            ]);

        var act = () => TurnoCreado.Crear(comando);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.OfType<ArgumentException>()
            .Should().ContainSingle(ae => ae.Message.Contains(TurnoCreado.Mensajes.FranjasOrdinariasSeSolapan));
    }

    [Fact]
    public void Crear_LanzaAggregateException_CuandoFranjasNocturnasSeSolapan()
    {
        // 22:00-06:00+1 se solapa con 23:00-07:00+1 (ambas cruzan medianoche)
        var comando = new CrearTurno(
            TurnoId,
            NombreValido,
            [
                new CrearTurno.Franja(new TimeOnly(22, 0), new TimeOnly(6, 0), [], []),
                new CrearTurno.Franja(new TimeOnly(23, 0), new TimeOnly(7, 0), [], [])
            ]);

        var act = () => TurnoCreado.Crear(comando);

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
        var comando = new CrearTurno(
            TurnoId,
            NombreValido,
            [new CrearTurno.Franja(
                new TimeOnly(8, 0), new TimeOnly(12, 0),
                [descansoFuera], [])]);

        var act = () => TurnoCreado.Crear(comando);

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
        var comando = new CrearTurno(TurnoId, "", []);

        var act = () => TurnoCreado.Crear(comando);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().HaveCount(2);
    }

    [Fact]
    public void Crear_LanzaAggregateExceptionConErroresPropiosYDelegados_CuandoNombreVacioYSubFranjaInvalida()
    {
        // Nombre vacio (error propio) + descanso fuera del contenedor (error delegado de VO)
        var descansoFuera = (new TimeOnly(14, 0), new TimeOnly(15, 0));
        var comando = new CrearTurno(
            TurnoId,
            "",
            [new CrearTurno.Franja(
                new TimeOnly(8, 0), new TimeOnly(12, 0),
                [descansoFuera], [])]);

        var act = () => TurnoCreado.Crear(comando);

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
        var comando = new CrearTurno(TurnoId, "", []);

        var act = () => TurnoCreado.Crear(comando);

        var ex = act.Should().ThrowExactly<AggregateException>().Which;
        ex.InnerExceptions.Should().AllBeAssignableTo<ArgumentException>();
    }
}
