using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections
// Solo TipoBloque es ambiguo entre los dos namespaces de arriba (CS0104): Bloque existe unicamente
// en ReadModels y BloqueTurno unicamente en DomainEvents, asi que ninguno de los dos necesita alias.
using TipoBloqueVigente = Bitakora.ControlAsistencia.ReadModels.ControlHoras.TipoBloque;
using TipoBloqueEvento = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.TipoBloque;

namespace Bitakora.ControlAsistencia.Projections.ControlHoras;

/// <summary>
/// Clase de proyeccion companion de TurnoVigente (issue #328, receta N1 -- un solo stream,
/// (EmpleadoId, Fecha), MEF-ADR-0035). Vive en el worker (Bitakora.ControlAsistencia.Projections),
/// el ensamblado que si referencia Marten y el analizador JasperFx.Events.SourceGenerator.
///
/// partial es obligatorio (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el build
/// queda limpio pero falla en RUNTIME al registrar la proyeccion (InvalidProjectionException) --
/// error que el config-test detecta al resolver el named store
/// (ConfigurarControlHoras_RegistraTurnoVigenteProjectionComoAsync).
///
/// Se registra en ConfiguracionMartenProjectionsControlHoras.ConfigurarControlHoras con
/// opts.Projections.Add&lt;TurnoVigenteProjection&gt;(ProjectionLifecycle.Async) -- lifecycle
/// canonico del worker (MEF-ADR-0034 seccion 3).
///
/// Create/Apply invocan evento.DetalleTurno.Segmentar(evento.Fecha) (issue #327, Tell-don't-Ask
/// MEF-ADR-0012: la aritmetica de segmentacion vive en TurnoDiario, no se reimplementa aqui) y
/// mapean cada BloqueTurno resultante al record Bloque propio de la vista (ReadModels, sin
/// relacion de tipo con DomainEvents -- alias TipoBloqueVigente/TipoBloqueEvento para desambiguar
/// el unico nombre homonimo entre ambos ensamblados, TipoBloque, CS0104).
///
/// Solo TurnoDiarioAsignado alimenta esta vista: MarcacionAdicionada tambien vive en el mismo
/// stream de ControlDiarioAggregateRoot pero esta proyeccion la ignora a proposito (issue #328,
/// "Eventos que la alimentan"). Sin ShouldDelete: el turno vigente nunca se borra, solo se
/// reasigna ("el ultimo gana", CA-2).
/// </summary>
public sealed partial class TurnoVigenteProjection : SingleStreamProjection<TurnoVigente, string>
{
    public static TurnoVigente Create(TurnoDiarioAsignado evento) =>
        new(
            evento.Id,
            evento.InformacionEmpleado.EmpleadoId,
            NombreCompleto(evento.InformacionEmpleado),
            evento.Fecha,
            evento.DetalleTurno.Nombre,
            evento.DetalleTurno.Descripcion,
            MapearBloques(evento));

    // CA-2: "el ultimo gana" -- una reasignacion sobre el mismo (empleado, fecha) sobrescribe
    // turno, horario y bloques. Id, EmpleadoId y Fecha no cambian: son la identidad del stream
    // ("{EmpleadoId}:{Fecha:yyyy-MM-dd}"), invariante para todos los eventos del mismo documento.
    //
    // NombreCompleto SI se refresca: cada TurnoDiarioAsignado trae el payload del colaborador
    // completo, y el criterio del "ultimo gana" aplica igual a un nombre corregido aguas arriba --
    // dejarlo fijo congelaria para siempre el nombre de la primera asignacion.
    public static TurnoVigente Apply(TurnoDiarioAsignado evento, TurnoVigente vista) =>
        vista with
        {
            NombreCompleto = NombreCompleto(evento.InformacionEmpleado),
            NombreTurno = evento.DetalleTurno.Nombre,
            HorarioResumido = evento.DetalleTurno.Descripcion,
            Bloques = MapearBloques(evento)
        };

    // Unico lugar del sistema donde se concatena Nombres + Apellidos (issue #328, "Investigacion
    // del planner"): el read model expone un solo campo de presentacion, no nombres/apellidos
    // separados.
    private static string NombreCompleto(ColaboradorProgramado colaborador) =>
        $"{colaborador.Nombres} {colaborador.Apellidos}";

    private static IReadOnlyList<Bloque> MapearBloques(TurnoDiarioAsignado evento) =>
        evento.DetalleTurno.Segmentar(evento.Fecha).Select(MapearBloque).ToList();

    // Issue #337: SedeId/NombreSede se propagan tal cual desde BloqueTurno.Sede (ya estampada por
    // TurnoDiario.Segmentar, issue #336) -- ambos quedan null cuando la franja de origen no trae
    // sede (turno prearmado sin resolver o evento anterior a #336).
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
