namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

/// <summary>
/// Envelope de respuesta (mismo patron que ListaAsistenciasDiarias/ListaTurnosVigentes): el rango
/// que declara es el efectivamente APLICADO -- ya recortado por <see cref="RangoConsulta.Recortar"/>
/// --, nunca el que pidio el cliente. Sin campo de cursor de pagina siguiente (patron #373): el
/// cliente deriva el proximo cursor del CodigoColaborador de la ultima fila; fin de lista = pagina
/// con menos de Take filas.
/// </summary>
public sealed record ListaResumenesAsistencia(
    DateOnly DesdeAplicado,
    DateOnly HastaAplicado,
    bool RangoRecortado,
    IReadOnlyList<ResumenAsistencia> Filas);
