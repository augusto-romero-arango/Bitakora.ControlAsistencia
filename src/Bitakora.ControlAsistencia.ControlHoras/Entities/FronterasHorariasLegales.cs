namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Referencia legal:
//   CST art. 160: inicio de la jornada diurna a las 6AM.
//   Ley 2466/2025 art. 10: inicio de la jornada nocturna a las 7PM.
public static class FronterasHorariasLegales
{
    /// <summary>Inicio de la banda diurna (6AM). La banda diurna es [InicioDiurna, InicioNocturna).</summary>
    public static readonly TimeOnly InicioDiurna = new(6, 0);

    /// <summary>Inicio de la banda nocturna (7PM). Marca el fin exclusivo de la banda diurna.</summary>
    public static readonly TimeOnly InicioNocturna = new(19, 0);
}
