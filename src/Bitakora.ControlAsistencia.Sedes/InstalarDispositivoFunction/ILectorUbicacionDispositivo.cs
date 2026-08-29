using Bitakora.ControlAsistencia.ReadModels.Sedes;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;

// Puerto del unico lookup cross-sede que el comando necesita (issue #477): rechazo barato antes de
// cargar el aggregate destino. Logica ramificada (comparar el SedeId de la vista contra el
// streamId destino) -- mismo criterio de ILectorSedesParaMarcacion
// (ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado): un fake manual la cubre en tests, no
// mockear Marten.
public interface ILectorUbicacionDispositivo
{
    Task<UbicacionDispositivo?> BuscarUbicacionAsync(string dispositivoId, CancellationToken ct = default);
}
