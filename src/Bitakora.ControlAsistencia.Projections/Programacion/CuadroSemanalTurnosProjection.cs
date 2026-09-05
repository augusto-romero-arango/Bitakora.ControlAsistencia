using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Clase de proyeccion companion de CuadroSemanalTurnos (receta N1: un solo stream, el de la
/// plantilla semanal PlantillaSemanalTurnos; MEF-ADR-0035, issue #624).
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
/// El retiro BORRA el cuadro (CA-ADR-0034 decision 4): el nombre queda libre para #626 y la
/// auditoria vive en el event store.
///
/// Implementacion pendiente de projection-implementer: los cuerpos son stub deliberado
/// (fase roja, projection-test-writer nunca implementa).
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

    // Apply nunca lanza (MEF-ADR-0004 capa 4): si el slot ya no existe, el Where simplemente no
    // quita nada y la vista queda igual.
    public static CuadroSemanalTurnos Apply(DiaDePlantillaSemanalQuitado e, CuadroSemanalTurnos vista) =>
        Reconstruir(vista, vista.Dias.Where(d => !CoincideSlot(d, e.Semana, e.Dia)));

    public static bool ShouldDelete(PlantillaSemanalRetirada e) => true;

    private static bool CoincideSlot(DiaDelCuadro dia, int semana, DiaSemana diaSemana) =>
        dia.Semana == semana && dia.Dia == diaSemana.Numero;

    // Unico punto de reconstruccion: ordena por (Semana, Dia) -- vista para leer lunes -> domingo
    // (MEF-ADR-0041), no el orden en que se asignaron los slots. Evita que Apply de asignado y de
    // quitado diverjan en el orden (mismo criterio que FichaTurnoProjection.Reconstruir).
    private static CuadroSemanalTurnos Reconstruir(CuadroSemanalTurnos vista, IEnumerable<DiaDelCuadro> dias) =>
        vista with
        {
            Dias = dias
                .OrderBy(d => d.Semana)
                .ThenBy(d => d.Dia)
                .ToList(),
        };
}
