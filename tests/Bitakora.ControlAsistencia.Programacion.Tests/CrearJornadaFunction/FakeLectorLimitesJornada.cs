using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;

internal sealed class FakeLectorLimitesJornada(
    FakeAseguradorJornadaPredeterminada? asegurador = null, params JornadaDelCatalogo[] jornadas)
    : ILectorLimitesJornada
{
    public bool AseguradorInvocadoAntesDeLeer { get; private set; }

    public Task<IReadOnlyList<JornadaDelCatalogo>> ObtenerLimitesAsync(Guid predeterminadaId, CancellationToken ct)
    {
        AseguradorInvocadoAntesDeLeer = asegurador is { Invocaciones: > 0 };
        return Task.FromResult<IReadOnlyList<JornadaDelCatalogo>>(jornadas);
    }
}
