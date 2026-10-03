using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections
// Solo TipoBloque es ambiguo entre los dos namespaces de arriba (CS0104): Bloque existe unicamente
// en ReadModels y BloqueTurno unicamente en DomainEvents, asi que ninguno de los dos necesita alias.
using TipoBloqueVigente = Bitakora.ControlAsistencia.ReadModels.ControlHoras.TipoBloque;
using TipoBloqueEvento = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.TipoBloque;

namespace Bitakora.ControlAsistencia.Projections.ControlHoras;

/// <summary>
/// Clase de proyeccion companion de TurnoVigente (receta N1 de MEF-ADR-0035: un solo stream por
/// (CodigoColaborador, Fecha)). Vive en el worker, el unico ensamblado que referencia Marten y el
/// analizador JasperFx.Events.SourceGenerator.
///
/// partial es OBLIGATORIO (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el build queda
/// limpio y falla en RUNTIME al registrar la proyeccion (InvalidProjectionException); el config-test
/// ConfigurarControlHoras_RegistraTurnoVigenteProjectionComoAsync es lo que lo detecta.
///
/// La aritmetica de segmentacion NO se reimplementa aqui: Create/Apply delegan en
/// evento.DetalleTurno.Segmentar(evento.Fecha) (Tell-don't-Ask, MEF-ADR-0012).
///
/// MarcacionAdicionada vive en el mismo stream y esta proyeccion la ignora a proposito. Un dia sin turno
/// ni ausencia se borra (Apply devuelve null).
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
            MapearBloques(evento));

    // "El ultimo gana": una reasignacion sobre el mismo (colaborador, fecha) sobrescribe turno,
    // horario y bloques. NombreCompleto SI se refresca (cada evento trae la terna de identidad).
    // Con una ausencia vigente el dia sigue mostrando la ausencia: el turno asignado solo actualiza
    // TurnoCubierto, que se restablece al cancelar la ausencia (CA-5).
    public static TurnoVigente Apply(TurnoDiarioAsignado evento, TurnoVigente vista) =>
        vista.AusenciaId is not null
            ? vista with
            {
                NombreCompleto = evento.InformacionColaborador.NombreCompleto,
                TurnoCubierto = new TurnoCubierto(
                    evento.DetalleTurno.Nombre, evento.DetalleTurno.Descripcion, MapearBloques(evento))
            }
            : vista with
            {
                NombreCompleto = evento.InformacionColaborador.NombreCompleto,
                NombreTurno = evento.DetalleTurno.Nombre,
                HorarioResumido = evento.DetalleTurno.Descripcion,
                Bloques = MapearBloques(evento)
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
            MotivoAusencia = evento.Motivo,
            AusenciaId = evento.AusenciaId,
            TurnoCubierto = vista.AusenciaId is not null
                ? vista.TurnoCubierto
                : new TurnoCubierto(vista.NombreTurno, vista.HorarioResumido, vista.Bloques)
        };

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
            MotivoAusencia = null,
            AusenciaId = null,
            TurnoCubierto = null
        };
    }

    private static IReadOnlyList<Bloque> MapearBloques(TurnoDiarioAsignado evento) =>
        evento.DetalleTurno.Segmentar(evento.Fecha).Select(MapearBloque).ToList();

    // SedeId/NombreSede quedan null cuando la franja de origen no trae sede (turno prearmado sin
    // resolver, o evento anterior a que Segmentar estampara la sede) -- null es un valor valido.
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
