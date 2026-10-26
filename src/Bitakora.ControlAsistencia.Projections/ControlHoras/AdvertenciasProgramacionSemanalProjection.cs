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
        Identity<CoberturaVecinaActualizada>(e => ClaveDesdeStream(e.Id, FechaDesdeStream(e.Id)));
        Identity<CierreDeLaVisperaActualizado>(e => ClaveDesdeStream(e.Id, FechaDesdeStream(e.Id)));
    }

    private const string PrefijoStream = "cd:";
    private const string FormatoFechaStream = "yyyyMMdd";

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

    private static DateOnly FechaDesdeStream(string streamId) =>
        DateOnly.ParseExact(streamId[(streamId.LastIndexOf(':') + 1)..], FormatoFechaStream, CultureInfo.InvariantCulture);

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
        var inicioCobertura = turno.Cobertura(evento.Fecha)?.Inicio;
        var aperturaTurno = turno.TieneVentanaDeEntrada ? null : turno.Apertura(evento.Fecha);

        var casilla = actual.AusenciaId is not null
            ? actual with
            {
                TurnoCubierto = new TurnoCubiertoSemana(turno.Nombre, minutos, esDescanso),
                InicioCobertura = inicioCobertura,
                AperturaTurno = aperturaTurno
            }
            : actual with
            {
                Tipo = esDescanso ? TipoCasilla.Descanso : TipoCasilla.Trabajo,
                NombreTurno = turno.Nombre,
                MinutosOrdinarios = minutos,
                InicioCobertura = inicioCobertura,
                AperturaTurno = aperturaTurno
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
            ? actual with { TurnoCubierto = null, InicioCobertura = null, AperturaTurno = null }
            : SinProgramar(actual.Fecha, actual.LimiteCoberturaVispera, actual.CierreTurnoVispera);

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
            ? SinProgramar(actual.Fecha, actual.LimiteCoberturaVispera, actual.CierreTurnoVispera)
            : new CasillaDia(
                actual.Fecha,
                cubierto.EsDescanso ? TipoCasilla.Descanso : TipoCasilla.Trabajo,
                cubierto.NombreTurno,
                cubierto.MinutosOrdinarios,
                InicioCobertura: actual.InicioCobertura,
                LimiteCoberturaVispera: actual.LimiteCoberturaVispera,
                AperturaTurno: actual.AperturaTurno,
                CierreTurnoVispera: actual.CierreTurnoVispera);

        return RecalcularOBorrar(vista, indice, casilla);
    }

    public static AdvertenciasProgramacionSemanal? Create(CoberturaVecinaActualizada evento) => null;

    public static AdvertenciasProgramacionSemanal Apply(CoberturaVecinaActualizada evento, AdvertenciasProgramacionSemanal vista)
    {
        if (evento.Vecino != DiaVecino.Vispera)
            return vista;

        var indice = Indice(vista, FechaDesdeStream(evento.Id));
        return Recalcular(vista, indice, vista.Casillas[indice] with { LimiteCoberturaVispera = evento.Limite });
    }

    public static AdvertenciasProgramacionSemanal? Create(CierreDeLaVisperaActualizado evento) => null;

    public static AdvertenciasProgramacionSemanal Apply(CierreDeLaVisperaActualizado evento, AdvertenciasProgramacionSemanal vista)
    {
        var indice = Indice(vista, FechaDesdeStream(evento.Id));
        return Recalcular(vista, indice, vista.Casillas[indice] with { CierreTurnoVispera = evento.Cierre });
    }

    private static int Indice(AdvertenciasProgramacionSemanal vista, DateOnly fecha) =>
        fecha.DayNumber - vista.Lunes.DayNumber;

    private static (int Anio, int Semana) SemanaIso(DateOnly fecha)
    {
        var dia = fecha.ToDateTime(TimeOnly.MinValue);
        return (ISOWeek.GetYear(dia), ISOWeek.GetWeekOfYear(dia));
    }

    private static CasillaDia SinProgramar(
        DateOnly fecha, DateTime? limiteCoberturaVispera = null, DateTime? cierreTurnoVispera = null) =>
        new(fecha, TipoCasilla.SinProgramar, "", 0,
            LimiteCoberturaVispera: limiteCoberturaVispera, CierreTurnoVispera: cierreTurnoVispera);

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
        var esJuzgable = sinProgramar == 0 && !tieneAusencias;
        var advertencias = CalculadorAdvertencias.Calcular(casillas, vista.Jornada, esJuzgable);
        var conAdvertencias = casillas
            .Select((c, i) => c with { Advertencias = advertencias.Diarias[i] })
            .ToList();
        return vista with
        {
            Casillas = conAdvertencias,
            MinutosOrdinariosProgramados = casillas.Sum(c => c.MinutosOrdinarios),
            DiasSinProgramar = sinProgramar,
            TieneAusencias = tieneAusencias,
            EsJuzgable = esJuzgable,
            AdvertenciasSemanales = advertencias.Semanales,
            TieneAdvertencias = advertencias.TieneAdvertencias
        };
    }
}
