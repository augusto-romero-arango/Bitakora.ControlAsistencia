using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Marten.Events.Projections;

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Receta N2 (MEF-ADR-0035): el stream es por colaborador y el documento por ausencia. partial es
/// obligatorio para el source generator de JasperFx.
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

    // null = sin dias vigentes: JasperFx (2.47.0) traduce snapshot null de un documento existente a
    // ActionType.Delete. No se usa ShouldDelete(e, vista): sobre el mismo evento que Apply el
    // generador emite un case duplicado (CS8120). Sin documento previo el evolver generado pasa un
    // objeto sin inicializar (TramosVigentes null); el ?? [] evita que Apply lance (MEF-ADR-0004).
    public static AusenciaVigente? Apply(AusenciaCancelada e, AusenciaVigente vista)
    {
        var canceladas = e.Fechas.ToHashSet();
        var tramos = new List<TramoDeAusencia>();

        foreach (var tramo in vista.TramosVigentes ?? [])
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
