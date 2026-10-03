using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Marten.Events.Projections; // MultiStreamProjection<,> vive aqui, NO en Marten.Events.Aggregation

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Clase de proyeccion companion de AusenciaVigente (receta N2: el stream es por colaborador y el
/// documento por ausencia; MEF-ADR-0035). partial es obligatorio (source generator de Marten).
/// </summary>
public sealed partial class AusenciaVigenteProjection : MultiStreamProjection<AusenciaVigente, Guid>
{
    public AusenciaVigenteProjection()
    {
        Identity<AusenciaProgramada>(e => e.AusenciaId);
        Identity<AusenciaCancelada>(e => e.AusenciaId);
    }

    public static AusenciaVigente Create(AusenciaProgramada e) =>
        throw new NotImplementedException();

    // Retorna null cuando la ausencia se queda sin dias vigentes: el evolver generado traduce
    // snapshot == null a ActionType.Delete. No se declara ShouldDelete(e, vista) junto a este Apply:
    // el generador de JasperFx emite un case duplicado para el mismo evento (CS8120) y ademas
    // cortocircuitaria el Apply.
    public static AusenciaVigente? Apply(AusenciaCancelada e, AusenciaVigente vista) =>
        throw new NotImplementedException();
}
