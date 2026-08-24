namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Envelope de respuesta de ListarAsistenciasDiarias (issue #427) -- mismo patron que
/// ListaTurnosVigentes (issue #329): declara el rango efectivamente aplicado (ya recortado por
/// <see cref="RangoConsulta.Recortar"/>, CA-3) junto con la lista de filas, una por dia del rango
/// (issue #427, "Que devuelve").
///
/// <c>Filas</c> nunca esta vacia -- CA-5: un rango sin ningun documento produce todas filas
/// sinteticas, nunca una lista vacia ni 404.
/// </summary>
public sealed record ListaAsistenciasDiarias(
    DateOnly DesdeAplicado,
    DateOnly HastaAplicado,
    bool RangoRecortado,
    IReadOnlyList<FilaAsistenciaDiaria> Filas);
