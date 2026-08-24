namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

/// <summary>
/// Envelope de respuesta (mismo patron que ListaTurnosVigentes): el rango que declara es el
/// efectivamente APLICADO -- ya recortado por <see cref="RangoConsulta.Recortar"/> --, nunca el
/// que pidio el cliente.
/// </summary>
public sealed record ListaAsistenciasDiarias(
    DateOnly DesdeAplicado,
    DateOnly HastaAplicado,
    bool RangoRecortado,
    IReadOnlyList<FilaAsistenciaDiaria> Filas);
