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
/// MarcacionAdicionada vive en el mismo stream y esta proyeccion la ignora a proposito. Sin
/// ShouldDelete: el turno vigente nunca se borra, solo se reasigna ("el ultimo gana").
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
    // horario y bloques. Id, CodigoColaborador y Fecha se omiten a proposito -- son la identidad del
    // stream, invariante para todos los eventos del documento.
    //
    // NombreCompleto SI se refresca: cada evento trae la terna de identidad, y dejarlo fijo
    // congelaria para siempre el nombre de la primera asignacion pese a una correccion aguas arriba.
    public static TurnoVigente Apply(TurnoDiarioAsignado evento, TurnoVigente vista) =>
        vista with
        {
            NombreCompleto = evento.InformacionColaborador.NombreCompleto,
            NombreTurno = evento.DetalleTurno.Nombre,
            HorarioResumido = evento.DetalleTurno.Descripcion,
            Bloques = MapearBloques(evento)
        };

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
