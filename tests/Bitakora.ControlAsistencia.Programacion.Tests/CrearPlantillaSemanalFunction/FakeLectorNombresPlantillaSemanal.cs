using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaSemanalFunction;

// Fake manual del puerto de lookup best-effort (MEF-ADR-0002): espejo de FakeLectorNombresTurno
// (#497) para el catalogo de plantillas semanales.
internal sealed class FakeLectorNombresPlantillaSemanal(params string[] nombres)
    : ILectorNombresPlantillaSemanal
{
    public Task<IReadOnlyList<string>> ObtenerNombresAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(nombres);
}
