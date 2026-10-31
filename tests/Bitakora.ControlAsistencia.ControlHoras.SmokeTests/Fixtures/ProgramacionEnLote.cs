namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

public static class ProgramacionEnLote
{
    public const string Topic = "programacion-turno-en-fechas-solicitada";

    public static readonly Guid JornadaId = Guid.Parse("0197a000-0000-7000-8000-000000001382");

    public static object Jornada { get; } = new
    {
        JornadaId,
        HorasSemanalesEnMinutos = 2880,
        TopeDiarioEnMinutos = 1440,
        MinimoDiarioEnMinutos = 0,
        DiasDescansoPorSemana = 1
    };
}
