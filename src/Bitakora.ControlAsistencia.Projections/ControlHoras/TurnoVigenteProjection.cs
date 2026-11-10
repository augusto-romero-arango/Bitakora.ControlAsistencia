using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections
// Solo TipoBloque existe en ambos namespaces (CS0104).
using TipoBloqueVigente = Bitakora.ControlAsistencia.ReadModels.ControlHoras.TipoBloque;
using TipoBloqueEvento = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.TipoBloque;

namespace Bitakora.ControlAsistencia.Projections.ControlHoras;

/// <summary>
/// partial es obligatorio: sin el, el source generator no emite el evolver y la proyeccion falla en
/// RUNTIME al registrarse (InvalidProjectionException), con el build limpio.
///
/// La segmentacion NO se reimplementa aqui: se delega en DetalleTurno.Segmentar (MEF-ADR-0012).
///
/// MarcacionAdicionada vive en el mismo stream y se ignora a proposito. Un dia sin turno ni ausencia
/// se borra (Apply devuelve null).
/// </summary>
public sealed partial class TurnoVigenteProjection : SingleStreamProjection<TurnoVigente, string>
{
    public static TurnoVigente Create(TurnoDiarioAsignado evento) =>
        new(
            evento.Id,
            evento.InformacionColaborador.CodigoColaborador,
            evento.InformacionColaborador.NombreCompleto,
            evento.Fecha,
            evento.DetalleTurno.Nombre,
            evento.DetalleTurno.Descripcion,
            MapearBloques(evento),
            EsHorarioAbierto: evento.DetalleTurno.EsHorarioAbierto);

    // El ultimo gana. Con una ausencia vigente el dia sigue mostrando la ausencia: el turno asignado
    // solo actualiza TurnoCubierto, que se restablece al cancelar la ausencia.
    public static TurnoVigente Apply(TurnoDiarioAsignado evento, TurnoVigente vista) =>
        vista.AusenciaId is not null
            ? vista with
            {
                NombreCompleto = evento.InformacionColaborador.NombreCompleto,
                TurnoCubierto = new TurnoCubierto(
                    evento.DetalleTurno.Nombre, evento.DetalleTurno.Descripcion, MapearBloques(evento),
                    evento.DetalleTurno.EsHorarioAbierto)
            }
            : vista with
            {
                NombreCompleto = evento.InformacionColaborador.NombreCompleto,
                NombreTurno = evento.DetalleTurno.Nombre,
                HorarioResumido = evento.DetalleTurno.Descripcion,
                Bloques = MapearBloques(evento),
                EsHorarioAbierto = evento.DetalleTurno.EsHorarioAbierto
            };

    // La ausencia cubre el turno sin reemplazarlo (CA-ADR-0036 decision 3): el dia se muestra como
    // ausencia, sin bloques, y el turno de debajo queda como estado interno.
    public static TurnoVigente Create(AusenciaDiariaAsignada evento) =>
        new(
            evento.Id,
            evento.Colaborador.CodigoColaborador,
            evento.Colaborador.NombreCompleto,
            evento.Fecha,
            "",
            "",
            [],
            evento.Motivo,
            evento.AusenciaId);

    public static TurnoVigente Apply(AusenciaDiariaAsignada evento, TurnoVigente vista) =>
        vista with
        {
            NombreCompleto = evento.Colaborador.NombreCompleto,
            NombreTurno = "",
            HorarioResumido = "",
            Bloques = [],
            EsHorarioAbierto = false,
            MotivoAusencia = evento.Motivo,
            AusenciaId = evento.AusenciaId,
            TurnoCubierto = vista.AusenciaId is not null
                ? vista.TurnoCubierto
                : new TurnoCubierto(
                    vista.NombreTurno, vista.HorarioResumido, vista.Bloques, vista.EsHorarioAbierto)
        };

    // Una cancelacion como primer evento del stream no crea documento: sin este Create el evolver
    // generado aplicaria Apply sobre un TurnoVigente sin inicializar.
    public static TurnoVigente? Create(TurnoDiarioCancelado evento) => null;

    public static TurnoVigente? Create(CancelacionAusenciaDiariaRegistrada evento) => null;

    // Apply devuelve null para borrar el documento (dia sin turno ni ausencia): Apply y ShouldDelete
    // del mismo evento no coexisten en el generador de Marten (CS8120).
    // Con ausencia vigente, el turno cancelado solo se descarta de TurnoCubierto.
    public static TurnoVigente? Apply(TurnoDiarioCancelado evento, TurnoVigente vista) =>
        vista.AusenciaId is not null ? vista with { TurnoCubierto = null } : null;

    // Una cancelacion de una ausencia que ya no es la vigente no altera la vista.
    public static TurnoVigente? Apply(CancelacionAusenciaDiariaRegistrada evento, TurnoVigente vista)
    {
        if (vista.AusenciaId != evento.AusenciaId)
            return vista;

        if (vista.TurnoCubierto is null)
            return null;

        return vista with
        {
            NombreTurno = vista.TurnoCubierto.NombreTurno,
            HorarioResumido = vista.TurnoCubierto.HorarioResumido,
            Bloques = vista.TurnoCubierto.Bloques,
            EsHorarioAbierto = vista.TurnoCubierto.EsHorarioAbierto,
            MotivoAusencia = null,
            AusenciaId = null,
            TurnoCubierto = null
        };
    }

    private static IReadOnlyList<Bloque> MapearBloques(TurnoDiarioAsignado evento) =>
        evento.DetalleTurno.Segmentar(evento.Fecha).Select(MapearBloque).ToList();

    private static Bloque MapearBloque(BloqueTurno bloque) =>
        new(MapearTipo(bloque.Tipo), bloque.Inicio, bloque.Fin, bloque.Sede?.Id, bloque.Sede?.Nombre);

    private static TipoBloqueVigente MapearTipo(TipoBloqueEvento tipo) => tipo switch
    {
        TipoBloqueEvento.Ordinaria => TipoBloqueVigente.Ordinaria,
        TipoBloqueEvento.Descanso => TipoBloqueVigente.Descanso,
        TipoBloqueEvento.Extra => TipoBloqueVigente.Extra,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoBloque sin mapeo a TipoBloqueVigente")
    };
}
