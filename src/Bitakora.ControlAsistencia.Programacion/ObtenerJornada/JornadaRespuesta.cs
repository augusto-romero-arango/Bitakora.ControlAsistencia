namespace Bitakora.ControlAsistencia.Programacion.ObtenerJornada;

public sealed record JornadaRespuesta(
    Guid JornadaId,
    HorasYMinutosRespuesta HorasSemanales,
    HorasYMinutosRespuesta TopeDiario,
    HorasYMinutosRespuesta MinimoDiario,
    int DiasDescansoPorSemana,
    string Descripcion,
    bool EsPredeterminada = false,
    bool Activa = true,
    string? Modalidad = null,
    bool DescansoAcumulado = false,
    IReadOnlyList<AdvertenciaJornadaRespuesta>? Advertencias = null)
{
    public IReadOnlyList<AdvertenciaJornadaRespuesta> Advertencias { get; init; } = Advertencias ?? [];
}

public sealed record AdvertenciaJornadaRespuesta(
    string Tipo,
    HorasYMinutosRespuesta Magnitud,
    HorasYMinutosRespuesta ValorDeLey,
    string Descripcion);
