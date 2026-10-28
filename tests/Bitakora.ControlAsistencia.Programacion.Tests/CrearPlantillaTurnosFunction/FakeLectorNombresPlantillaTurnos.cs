using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaTurnosFunction;

internal sealed class FakeLectorNombresPlantillaTurnos(params string[] nombres)
    : ILectorNombresPlantillaTurnos
{
    public Task<IReadOnlyList<string>> ObtenerNombresAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(nombres);
}
