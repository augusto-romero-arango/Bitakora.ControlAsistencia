using System.Globalization;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Projections; // MultiStreamProjection<,> vive aqui

namespace Bitakora.ControlAsistencia.Projections.ControlHoras;

/// <summary>Proyeccion companion N2 de AdvertenciasProgramacionSemanal: agrupa los streams diarios por (colaborador, semana ISO).</summary>
public sealed partial class AdvertenciasProgramacionSemanalProjection
    : MultiStreamProjection<AdvertenciasProgramacionSemanal, string>
{
    public AdvertenciasProgramacionSemanalProjection()
    {
        Identity<TurnoDiarioAsignado>(e => Clave(e.InformacionColaborador.CodigoColaborador, e.Fecha));
        Identity<TurnoDiarioCancelado>(e => Clave(e.Colaborador.CodigoColaborador, e.Fecha));
        Identity<AusenciaDiariaAsignada>(e => Clave(e.Colaborador.CodigoColaborador, e.Fecha));
        Identity<CancelacionAusenciaDiariaRegistrada>(e => ClaveDesdeStream(e.Id, e.Fecha));
    }

    private const string PrefijoStream = "cd:";

    public static string Clave(string codigoColaborador, DateOnly fecha)
    {
        var (anio, semana) = SemanaIso(fecha);
        return $"{codigoColaborador}:{anio}-W{semana:00}";
    }

    // Stream key del write-side: "cd:{CodigoColaborador}:{yyyyMMdd}" (ControlDiarioAggregateRoot.ComputarStreamId).
    public static string ClaveDesdeStream(string streamId, DateOnly fecha)
    {
        var inicio = streamId.StartsWith(PrefijoStream, StringComparison.Ordinal) ? PrefijoStream.Length : 0;
        var fin = streamId.LastIndexOf(':');
        var codigo = fin >= inicio ? streamId[inicio..fin] : streamId[inicio..];
        return Clave(codigo, fecha);
    }

    public static AdvertenciasProgramacionSemanal Create(TurnoDiarioAsignado evento) =>
        Apply(evento, Nueva(
            evento.InformacionColaborador.CodigoColaborador,
            evento.InformacionColaborador.NombreCompleto,
            evento.Fecha));

    public static AdvertenciasProgramacionSemanal Apply(TurnoDiarioAsignado evento, AdvertenciasProgramacionSemanal vista)
    {
        var turno = evento.DetalleTurno;
        var minutos = turno.MinutosOrdinarios();
        var esDescanso = turno.FranjasOrdinarias.Count == 0;
        var indice = Indice(vista, evento.Fecha);
        var actual = vista.Casillas[indice];

        var casilla = actual.AusenciaId is not null
            ? actual with { TurnoCubierto = new TurnoCubiertoSemana(turno.Nombre, minutos, esDescanso) }
            : actual with
            {
                Tipo = esDescanso ? TipoCasilla.Descanso : TipoCasilla.Trabajo,
                NombreTurno = turno.Nombre,
                MinutosOrdinarios = minutos
            };

        var jornada = evento.Jornada is null
            ? vista.Jornada
            : new JornadaAplicada(
                evento.Jornada.JornadaId,
                evento.Jornada.HorasSemanalesEnMinutos,
                evento.Jornada.TopeDiarioEnMinutos,
                evento.Jornada.MinimoDiarioEnMinutos,
                evento.Jornada.DiasDescansoPorSemana);

        return Recalcular(
            vista with { NombreCompleto = evento.InformacionColaborador.NombreCompleto, Jornada = jornada },
            indice,
            casilla);
    }

    public static AdvertenciasProgramacionSemanal Create(AusenciaDiariaAsignada evento) =>
        Apply(evento, Nueva(evento.Colaborador.CodigoColaborador, evento.Colaborador.NombreCompleto, evento.Fecha));

    public static AdvertenciasProgramacionSemanal Apply(AusenciaDiariaAsignada evento, AdvertenciasProgramacionSemanal vista)
    {
        var indice = Indice(vista, evento.Fecha);
        var actual = vista.Casillas[indice];

        var cubierto = actual.AusenciaId is not null
            ? actual.TurnoCubierto
            : actual.Tipo is TipoCasilla.Trabajo or TipoCasilla.Descanso
                ? new TurnoCubiertoSemana(actual.NombreTurno, actual.MinutosOrdinarios, actual.Tipo == TipoCasilla.Descanso)
                : null;

        var casilla = actual with
        {
            Tipo = TipoCasilla.Ausencia,
            NombreTurno = "",
            MinutosOrdinarios = 0,
            MotivoAusencia = evento.Motivo,
            AusenciaId = evento.AusenciaId,
            TurnoCubierto = cubierto
        };

        return Recalcular(vista with { NombreCompleto = evento.Colaborador.NombreCompleto }, indice, casilla);
    }

    // Una cancelacion como primer evento (#747) no crea documento.
    public static AdvertenciasProgramacionSemanal? Create(TurnoDiarioCancelado evento) => null;

    public static AdvertenciasProgramacionSemanal? Apply(TurnoDiarioCancelado evento, AdvertenciasProgramacionSemanal vista)
    {
        var indice = Indice(vista, evento.Fecha);
        var actual = vista.Casillas[indice];

        var casilla = actual.AusenciaId is not null
            ? actual with { TurnoCubierto = null }
            : SinProgramar(actual.Fecha);

        return RecalcularOBorrar(vista with { NombreCompleto = evento.Colaborador.NombreCompleto }, indice, casilla);
    }

    public static AdvertenciasProgramacionSemanal? Create(CancelacionAusenciaDiariaRegistrada evento) => null;

    public static AdvertenciasProgramacionSemanal? Apply(CancelacionAusenciaDiariaRegistrada evento, AdvertenciasProgramacionSemanal vista)
    {
        var indice = Indice(vista, evento.Fecha);
        var actual = vista.Casillas[indice];

        if (actual.AusenciaId != evento.AusenciaId)
            return vista;

        var cubierto = actual.TurnoCubierto;
        var casilla = cubierto is null
            ? SinProgramar(actual.Fecha)
            : new CasillaDia(
                actual.Fecha,
                cubierto.EsDescanso ? TipoCasilla.Descanso : TipoCasilla.Trabajo,
                cubierto.NombreTurno,
                cubierto.MinutosOrdinarios);

        return RecalcularOBorrar(vista, indice, casilla);
    }

    private static int Indice(AdvertenciasProgramacionSemanal vista, DateOnly fecha) =>
        fecha.DayNumber - vista.Lunes.DayNumber;

    private static (int Anio, int Semana) SemanaIso(DateOnly fecha)
    {
        var dia = fecha.ToDateTime(TimeOnly.MinValue);
        return (ISOWeek.GetYear(dia), ISOWeek.GetWeekOfYear(dia));
    }

    private static CasillaDia SinProgramar(DateOnly fecha) => new(fecha, TipoCasilla.SinProgramar, "", 0);

    private static AdvertenciasProgramacionSemanal Nueva(string codigo, string nombre, DateOnly fecha)
    {
        var (anio, semana) = SemanaIso(fecha);
        var lunes = DateOnly.FromDateTime(ISOWeek.ToDateTime(anio, semana, DayOfWeek.Monday));
        var casillas = Enumerable.Range(0, 7).Select(i => SinProgramar(lunes.AddDays(i))).ToList();
        return new AdvertenciasProgramacionSemanal(
            Clave(codigo, fecha), codigo, nombre, anio, semana, lunes, lunes.AddDays(6),
            null, 0, 7, false, false, casillas);
    }

    private static AdvertenciasProgramacionSemanal? RecalcularOBorrar(
        AdvertenciasProgramacionSemanal vista, int indice, CasillaDia casilla)
    {
        var resultado = Recalcular(vista, indice, casilla);
        return resultado.Casillas.All(c => c.Tipo == TipoCasilla.SinProgramar && c.AusenciaId is null)
            ? null
            : resultado;
    }

    // Estado derivado: se recalcula desde las casillas, nunca como acumulador.
    private static AdvertenciasProgramacionSemanal Recalcular(
        AdvertenciasProgramacionSemanal vista, int indice, CasillaDia casilla)
    {
        var casillas = vista.Casillas.ToList();
        casillas[indice] = casilla;
        var sinProgramar = casillas.Count(c => c.Tipo == TipoCasilla.SinProgramar);
        var tieneAusencias = casillas.Any(c => c.Tipo == TipoCasilla.Ausencia);
        return vista with
        {
            Casillas = casillas,
            MinutosOrdinariosProgramados = casillas.Sum(c => c.MinutosOrdinarios),
            DiasSinProgramar = sinProgramar,
            TieneAusencias = tieneAusencias,
            EsJuzgable = sinProgramar == 0 && !tieneAusencias
        };
    }
}
