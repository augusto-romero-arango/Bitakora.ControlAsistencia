using Bitakora.ControlAsistencia.Programacion.Infraestructura;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;

internal sealed class FakeAseguradorJornadaPredeterminada(Guid jornadaId) : IAseguradorJornadaPredeterminada
{
    public int Invocaciones { get; private set; }

    public Task<Guid> AsegurarAsync(CancellationToken ct)
    {
        Invocaciones++;
        return Task.FromResult(jornadaId);
    }
}
