// Issue #429: unit tests puros (sin Marten) de DiaCalculadoAggregateRoot.GenerarDepuracionDelDia()
// -- el metodo generador que produce la vista DepuracionDelDia (via (b1),
// skills/projections/read-apis.md). Se instancia el aggregate directamente y se le aplica
// DepuracionDiaRecibida via Apply (publico -- "requerido para que TestStore.ApplyEvent lo encuentre
// via GetMethods()", ver el propio archivo de produccion) -- sin AggregateStreamAsync, sin Postgres:
// es exactamente como el aggregate se hidrata en produccion, reaplicando el mismo evento (mecanismo
// Live, MEF-ADR-0015), solo que aqui se hace a mano en vez de sobre un stream real.
//
// Cada assert arma el esperado a mano (oraculo independiente, MEF-ADR-0002) -- nunca reusa la logica
// del generador bajo prueba. CA-1..CA-4 del issue #429.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using DepuracionDiaRecibida = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.DepuracionDiaRecibida;
using EventoFranjaDepurada = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.FranjaDepurada;
using EventoMarcacionDelDia = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.MarcacionDelDia;
using HorasDiscriminadas = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.HorasDiscriminadas;
using ResumenColaborador = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class DiaCalculadoAggregateRootTests
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 8, 24);

    private static DiaCalculadoAggregateRoot HidratarCon(DepuracionDiaRecibida evento)
    {
        var dia = new DiaCalculadoAggregateRoot();
        dia.Apply(evento);
        return dia;
    }

    private static DepuracionDiaRecibida Evento(
        ResumenColaborador? colaborador,
        string? nombreTurno,
        IReadOnlyList<EventoFranjaDepurada> franjas,
        IReadOnlyList<EventoMarcacionDelDia> marcaciones,
        HorasDiscriminadas horas)
    {
        var streamId = DiaCalculadoAggregateRoot.ComputarStreamId(CodigoColaborador, Fecha);
        return new DepuracionDiaRecibida(
            streamId, CodigoColaborador, Fecha, colaborador, nombreTurno, franjas, marcaciones, horas);
    }

    // CA-1: dia con jornada valida -> 200 con la vista completa: identidad + foto del colaborador,
    // Plan=ConJornada + NombreTurno, franjas plan/realidad, TODAS las marcaciones en orden
    // cronologico, HorasPorConcepto sparse, Trazabilidad y Estado=Provisional. WithStrictOrdering
    // porque el contrato del evento exige orden cronologico de las marcaciones (issue #429,
    // "Necesidad de lectura") -- BeEquivalentTo por defecto ignora el orden de las colecciones.
    [Fact]
    public void GenerarDepuracionDelDia_ProduceLaVistaCompleta_CuandoElDiaTieneJornadaValida()
    {
        var entrada = new DateTime(2026, 8, 24, 6, 2, 0);
        var salida = new DateTime(2026, 8, 24, 14, 5, 0);
        var franja = new EventoFranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, salida, false);
        var marcacionEntrada = new EventoMarcacionDelDia(entrada, "Entrada");
        var marcacionSalida = new EventoMarcacionDelDia(salida, "Salida");
        var colaborador = new ResumenColaborador("CC-79543210", CodigoColaborador, "Ana Torres");
        var horas = new HorasDiscriminadas(
            new Dictionary<string, decimal> { ["Ordinaria"] = 8m },
            ["Se tomo la franja unica del turno Manana"]);

        var dia = HidratarCon(Evento(colaborador, "Manana", [franja], [marcacionEntrada, marcacionSalida], horas));

        var vista = dia.GenerarDepuracionDelDia();

        var esperado = new DepuracionDelDia(
            CodigoColaborador,
            Fecha,
            "CC-79543210",
            "Ana Torres",
            EstadoAsistencia.Provisional,
            PlanDelDia.ConJornada,
            "Manana",
            [new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, salida, false)],
            [
                new MarcacionDelDia(entrada, "Entrada", true),
                new MarcacionDelDia(salida, "Salida", true)
            ],
            new Dictionary<string, decimal> { ["Ordinaria"] = 8m },
            ["Se tomo la franja unica del turno Manana"]);

        vista.Should().BeEquivalentTo(esperado, opciones => opciones.WithStrictOrdering());
    }

    // CA-2: marcacion cuyo Timestamp coincide EXACTAMENTE con la Entrada o Salida de alguna
    // franja -> Usada=true.
    [Fact]
    public void GenerarDepuracionDelDia_MarcaUsadaVerdadero_CuandoElTimestampCoincideConEntradaOSalidaDeUnaFranja()
    {
        var entrada = new DateTime(2026, 8, 24, 6, 0, 0);
        var salida = new DateTime(2026, 8, 24, 14, 0, 0);
        var franja = new EventoFranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, salida, false);
        IReadOnlyList<EventoMarcacionDelDia> marcaciones =
            [new EventoMarcacionDelDia(entrada, "Entrada"), new EventoMarcacionDelDia(salida, "Salida")];
        var horas = new HorasDiscriminadas(new Dictionary<string, decimal>(), []);

        var dia = HidratarCon(Evento(null, "Manana", [franja], marcaciones, horas));

        var vista = dia.GenerarDepuracionDelDia();

        vista.Marcaciones.Should().OnlyContain(m => m.Usada);
    }

    // CA-2: las demas -- descartadas visibles -- quedan con Usada=false. La marcacion esta a un
    // minuto de la Entrada real: cerca, pero sin igualdad exacta.
    [Fact]
    public void GenerarDepuracionDelDia_MarcaUsadaFalso_CuandoElTimestampNoCoincideConNingunaFranja()
    {
        var entrada = new DateTime(2026, 8, 24, 6, 0, 0);
        var salida = new DateTime(2026, 8, 24, 14, 0, 0);
        var franja = new EventoFranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, salida, false);
        var marcacionDescartada = new EventoMarcacionDelDia(new DateTime(2026, 8, 24, 6, 1, 0), "Entrada");
        var horas = new HorasDiscriminadas(new Dictionary<string, decimal>(), []);

        var dia = HidratarCon(Evento(null, "Manana", [franja], [marcacionDescartada], horas));

        var vista = dia.GenerarDepuracionDelDia();

        vista.Marcaciones.Should().ContainSingle().Which.Usada.Should().BeFalse();
    }

    // CA-3: NombreTurno null -> Plan=SinProgramar (marcaciones todas con Usada=false, sin franjas,
    // horas vacias).
    [Fact]
    public void GenerarDepuracionDelDia_DerivaPlanSinProgramar_CuandoNombreTurnoEsNulo()
    {
        IReadOnlyList<EventoMarcacionDelDia> marcaciones = [new EventoMarcacionDelDia(new DateTime(2026, 8, 24, 9, 0, 0), "Entrada")];
        var horas = new HorasDiscriminadas(new Dictionary<string, decimal>(), []);

        var dia = HidratarCon(Evento(null, null, [], marcaciones, horas));

        var vista = dia.GenerarDepuracionDelDia();

        vista.Plan.Should().Be(PlanDelDia.SinProgramar);
        vista.NombreTurno.Should().BeNull();
        vista.Franjas.Should().BeEmpty();
        vista.Marcaciones.Should().OnlyContain(m => !m.Usada);
        vista.HorasPorConcepto.Should().BeEmpty();
    }

    // CA-3: nombre + cero franjas -> Plan=Descanso.
    [Fact]
    public void GenerarDepuracionDelDia_DerivaPlanDescanso_CuandoHayNombreTurnoYCeroFranjas()
    {
        var horas = new HorasDiscriminadas(new Dictionary<string, decimal>(), []);

        var dia = HidratarCon(Evento(null, "Descanso semanal", [], [], horas));

        var vista = dia.GenerarDepuracionDelDia();

        vista.Plan.Should().Be(PlanDelDia.Descanso);
        vista.NombreTurno.Should().Be("Descanso semanal");
        vista.Franjas.Should().BeEmpty();
    }

    // CA-4: dia que nacio solo por marcacion (sin ResumenColaborador en el evento) ->
    // IdentificacionColaborador y NombreColaborador null, el resto de la vista integro.
    [Fact]
    public void GenerarDepuracionDelDia_DejaNulaLaFotoDelColaborador_CuandoElEventoNoTraeResumenColaborador()
    {
        var entrada = new DateTime(2026, 8, 24, 6, 0, 0);
        var franja = new EventoFranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, null, true);
        IReadOnlyList<EventoMarcacionDelDia> marcaciones = [new EventoMarcacionDelDia(entrada, "Entrada")];
        var horas = new HorasDiscriminadas(
            new Dictionary<string, decimal> { ["Ordinaria"] = 4m }, ["Solo se registro la entrada"]);

        var dia = HidratarCon(Evento(null, "Manana", [franja], marcaciones, horas));

        var vista = dia.GenerarDepuracionDelDia();

        vista.IdentificacionColaborador.Should().BeNull();
        vista.NombreColaborador.Should().BeNull();
        vista.CodigoColaborador.Should().Be(CodigoColaborador);
        vista.Fecha.Should().Be(Fecha);
        vista.Plan.Should().Be(PlanDelDia.ConJornada);
        vista.NombreTurno.Should().Be("Manana");
        vista.Estado.Should().Be(EstadoAsistencia.Provisional);
        vista.Franjas.Should().ContainSingle().Which.EsAnomala.Should().BeTrue();
    }
}
