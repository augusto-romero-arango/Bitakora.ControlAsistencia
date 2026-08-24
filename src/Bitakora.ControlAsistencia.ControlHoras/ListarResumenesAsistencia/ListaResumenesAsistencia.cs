namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// Envelope de respuesta. El rango que declara es el efectivamente APLICADO -- ya recortado por
/// <see cref="RangoConsulta.Recortar"/> --, nunca el que pidio el cliente. Sin campo de cursor de
/// pagina siguiente: el cliente lo deriva del CodigoColaborador de la ultima fila, y el fin de
/// lista es una pagina con menos de Take filas.
/// </summary>
public sealed record ListaResumenesAsistencia(
    DateOnly DesdeAplicado,
    DateOnly HastaAplicado,
    bool RangoRecortado,
    IReadOnlyList<ResumenAsistencia> Filas);
