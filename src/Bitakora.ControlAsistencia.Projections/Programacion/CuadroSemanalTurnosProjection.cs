using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Clase de proyeccion companion de CuadroSemanalTurnos (receta N1: un solo stream, el de la
/// plantilla semanal PlantillaSemanalTurnos; MEF-ADR-0035).
/// </summary>
/// <remarks>
/// partial es obligatorio: el source generator descubre Create/Apply por convencion y emite el
/// dispatcher [GeneratedEvolver]. Sin partial el build queda limpio pero falla en RUNTIME al
/// registrar la proyeccion (InvalidProjectionException).
///
/// Create toma IEvent&lt;PlantillaSemanalCreada&gt;, no PlantillaSemanalCreada a secas: la
/// identidad del documento es exactamente el StreamKey del stream de la plantilla, nunca
/// recomputada a mano desde el payload.
///
/// El retiro BORRA el cuadro (CA-ADR-0034 decision 4): el nombre queda libre para reusarse y la
/// auditoria vive en el event store.
/// </remarks>
public sealed partial class CuadroSemanalTurnosProjection
    : SingleStreamProjection<CuadroSemanalTurnos, string>
{
    public static CuadroSemanalTurnos Create(IEvent<PlantillaSemanalCreada> e) =>
        new(e.StreamKey!, e.Data.Nombre, e.Data.Semanas, []);

    public static CuadroSemanalTurnos Apply(DiaDePlantillaSemanalAsignado e, CuadroSemanalTurnos vista) =>
        Reconstruir(vista, vista.Dias
            .Where(d => !CoincideSlot(d, e.Semana, e.Dia))
            .Append(new DiaDelCuadro(e.Semana, e.Dia.Numero, e.TurnoId.ToString())));

    // Apply nunca lanza (MEF-ADR-0004 capa 4): un slot ausente deja la vista igual.
    public static CuadroSemanalTurnos Apply(DiaDePlantillaSemanalQuitado e, CuadroSemanalTurnos vista) =>
        Reconstruir(vista, vista.Dias.Where(d => !CoincideSlot(d, e.Semana, e.Dia)));

    public static bool ShouldDelete(PlantillaSemanalRetirada e) => true;

    private static bool CoincideSlot(DiaDelCuadro dia, int semana, DiaSemana diaSemana) =>
        dia.Semana == semana && dia.Dia == diaSemana.Numero;

    // Unico punto de reconstruccion, para que asignado y quitado no diverjan en el orden: la vista
    // se lee lunes -> domingo (MEF-ADR-0041), no en el orden en que se asignaron los slots.
    private static CuadroSemanalTurnos Reconstruir(CuadroSemanalTurnos vista, IEnumerable<DiaDelCuadro> dias) =>
        vista with
        {
            Dias = dias
                .OrderBy(d => d.Semana)
                .ThenBy(d => d.Dia)
                .ToList(),
        };
}
