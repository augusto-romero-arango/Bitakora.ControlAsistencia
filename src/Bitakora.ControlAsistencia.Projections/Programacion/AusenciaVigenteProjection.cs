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
        new(
            e.AusenciaId,
            e.Colaborador.CodigoColaborador,
            e.Colaborador.NombreCompleto,
            e.Motivo.Nombre,
            [new TramoDeAusencia(e.FechaInicio, e.FechaFin)],
            e.FechaInicio,
            e.FechaFin);

    // Retorna null cuando la ausencia se queda sin dias vigentes: el evolver generado traduce
    // snapshot == null a ActionType.Delete. No se declara ShouldDelete(e, vista) junto a este Apply:
    // el generador de JasperFx emite un case duplicado para el mismo evento (CS8120) y ademas
    // cortocircuitaria el Apply.
    public static AusenciaVigente? Apply(AusenciaCancelada e, AusenciaVigente vista)
    {
        var canceladas = e.Fechas.ToHashSet();
        var tramos = new List<TramoDeAusencia>();

        foreach (var tramo in vista.TramosVigentes)
        {
            DateOnly? inicio = null;
            for (var dia = tramo.Desde; dia <= tramo.Hasta; dia = dia.AddDays(1))
            {
                if (canceladas.Contains(dia))
                {
                    if (inicio is { } i)
                    {
                        tramos.Add(new TramoDeAusencia(i, dia.AddDays(-1)));
                        inicio = null;
                    }
                }
                else
                {
                    inicio ??= dia;
                }
            }
            if (inicio is { } ultimo)
                tramos.Add(new TramoDeAusencia(ultimo, tramo.Hasta));
        }

        if (tramos.Count == 0)
            return null;

        return vista with
        {
            TramosVigentes = tramos,
            PrimerDiaVigente = tramos[0].Desde,
            UltimoDiaVigente = tramos[^1].Hasta,
        };
    }
}
