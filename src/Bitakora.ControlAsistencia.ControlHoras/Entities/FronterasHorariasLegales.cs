namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Issue #134: Fronteras horarias legales colombianas que delimitan la banda diurna/nocturna.
// Recreado como lo previo el review de #115 (commit 4834c04 "la HU que la requiera la creara
// con la firma exacta que su primer consumidor real necesite"). El primer consumidor real es
// ClasificadorHorario.ClasificarBanda, que solo necesita los dos umbrales de banda; por eso
// no se reintroducen Medianoche ni la combinacion Todas (sin consumidor en este alcance).
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
