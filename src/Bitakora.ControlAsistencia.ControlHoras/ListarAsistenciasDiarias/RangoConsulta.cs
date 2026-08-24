namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Resultado de aplicar la cota de <see cref="RangoConsulta.CotaDias"/> sobre el rango de fechas
/// pedido a ListarAsistenciasDiarias (issue #427, CA-3).
/// </summary>
public readonly record struct RangoAplicado(DateOnly HastaAplicado, bool RangoRecortado);

/// <summary>
/// Logica pura de recorte del rango de consulta de ListarAsistenciasDiarias (issue #427, CA-3) --
/// misma politica exacta que RangoConsulta de ListarTurnosVigentes (issue #329): 31 dias inclusive,
/// recorte SIEMPRE hacia adelante desde <c>desde</c>, nunca relativo a la fecha de hoy.
///
/// Duplicada a proposito (SEGUNDA aparicion de esta politica en el dominio -- MEF-ADR-0018 Rule of
/// Three): el issue #427 la deja explicitamente revisable ("Recorte de rango -- propuesta
/// revisable") y con solo dos instancias duplicar en el feature folder propio sigue siendo
/// legitimo -- reusar la clase de ListarTurnosVigentes cruzaria fronteras de feature folder
/// (skills/projections/naming.md: cada Function GET/QUERY es un feature folder propio). Un tercer
/// consumidor de esta misma politica es quien decide si se extrae a un lugar comun del dominio.
///
/// Sin dependencias de Marten/Postgres: se prueba como funcion pura sobre DateOnly, sin
/// QuerySession (skills/projections/read-apis.md).
/// </summary>
public static class RangoConsulta
{
    /// <summary>Cota maxima del rango, en dias, INCLUSIVE (CA-3): desde y desde + 30 dias caben.</summary>
    public const int CotaDias = 31;

    public static RangoAplicado Recortar(DateOnly desde, DateOnly hasta)
    {
        throw new NotImplementedException();
    }
}
