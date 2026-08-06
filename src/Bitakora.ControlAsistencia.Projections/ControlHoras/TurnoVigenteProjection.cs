using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections
using BloqueVigente = Bitakora.ControlAsistencia.ReadModels.ControlHoras.Bloque;
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
/// canonico del worker (MEF-ADR-0034 seccion 3), sumado aditivamente junto a TurnoDiarioProjection
/// (issue #289) en el mismo AddMartenStore.
///
/// Create/Apply invocan evento.DetalleTurno.Segmentar(evento.Fecha) (issue #327, Tell-don't-Ask
/// MEF-ADR-0012: la aritmetica de segmentacion vive en TurnoDiario, no se reimplementa aqui) y
/// mapean cada BloqueTurno resultante al record Bloque propio de la vista (ReadModels, sin
/// relacion de tipo con DomainEvents -- alias BloqueVigente/TipoBloqueVigente para desambiguar del
/// TipoBloque homonimo de ControlHoras.DomainEvents, CS0104).
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
    // turno, horario y bloques. Id, EmpleadoId, NombreCompleto y Fecha no cambian (mismo stream,
    // mismo empleado): el evento no trae informacion distinta de empleado en una reasignacion.
    public static TurnoVigente Apply(TurnoDiarioAsignado evento, TurnoVigente vista) =>
        vista with
        {
            NombreTurno = evento.DetalleTurno.Nombre,
            HorarioResumido = evento.DetalleTurno.Descripcion,
            Bloques = MapearBloques(evento)
        };

    // Unico lugar del sistema donde se concatena Nombres + Apellidos (issue #328, "Investigacion
    // del planner"): el read model expone un solo campo de presentacion, no nombres/apellidos
    // separados.
    private static string NombreCompleto(Empleado empleado) =>
        $"{empleado.Nombres} {empleado.Apellidos}";

    private static IReadOnlyList<BloqueVigente> MapearBloques(TurnoDiarioAsignado evento) =>
        evento.DetalleTurno.Segmentar(evento.Fecha).Select(MapearBloque).ToList();

    private static BloqueVigente MapearBloque(BloqueTurno bloque) =>
        new(MapearTipo(bloque.Tipo), bloque.Inicio, bloque.Fin);

    private static TipoBloqueVigente MapearTipo(TipoBloqueEvento tipo) => tipo switch
    {
        TipoBloqueEvento.Ordinaria => TipoBloqueVigente.Ordinaria,
        TipoBloqueEvento.Descanso => TipoBloqueVigente.Descanso,
        TipoBloqueEvento.Extra => TipoBloqueVigente.Extra,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoBloque sin mapeo a TipoBloqueVigente")
    };
}
