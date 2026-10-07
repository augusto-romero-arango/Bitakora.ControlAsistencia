namespace Bitakora.ControlAsistencia.Programacion.ObtenerJornada;

public sealed record JornadaRespuesta(
    Guid JornadaId,
    HorasYMinutosRespuesta HorasSemanales,
    HorasYMinutosRespuesta TopeDiario,
    HorasYMinutosRespuesta MinimoDiario,
    int DiasDescansoPorSemana,
    string Descripcion);
