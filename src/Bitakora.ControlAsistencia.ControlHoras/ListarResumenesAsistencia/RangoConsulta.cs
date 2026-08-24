namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// Rango efectivamente aplicado tras acotar el pedido a <see cref="RangoConsulta.CotaDias"/>.
/// </summary>
public readonly record struct RangoAplicado(DateOnly HastaAplicado, bool RangoRecortado);

/// <summary>
/// Recorte del rango de consulta: SIEMPRE hacia adelante desde <c>desde</c> -- nunca hacia atras
/// desde <c>hasta</c> ni relativo a la fecha de hoy, que haria que la misma consulta devolviera
/// datos distintos segun el dia en que se ejecuta.
///
/// TERCERA aparicion de esta politica en ControlHoras (ListarTurnosVigentes, #329;
/// ListarAsistenciasDiarias, #427; este feature, #428) -- Rule of Three (MEF-ADR-0018): el propio
/// issue lo marca como "propuesta revisable" y deja la extraccion a un lugar comun del dominio como
/// decision del pipeline (fase verde), no del test-writer -- mover codigo de produccion ya
/// implementado en los dos feature folders anteriores es implementacion, fuera del alcance de este
/// agente (ver resumen de stage, "Desviaciones del plan del planner"). Este archivo es el STUB
/// minimo de compilacion de la fase roja: NotImplementedException hasta que projection-implementer
/// decida duplicar la logica una tercera vez o ejecutar la extraccion y actualizar los tres
/// consumidores.
/// </summary>
public static class RangoConsulta
{
    /// <summary>Cota maxima del rango, en dias, INCLUSIVE: desde y desde + 30 dias caben.</summary>
    public const int CotaDias = 31;

    public static RangoAplicado Recortar(DateOnly desde, DateOnly hasta)
    {
        var hastaMaxima = desde.AddDays(CotaDias - 1);

        return hasta > hastaMaxima
            ? new RangoAplicado(hastaMaxima, true)
            : new RangoAplicado(hasta, false);
    }
}
