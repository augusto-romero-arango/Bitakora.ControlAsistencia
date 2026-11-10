using Bitakora.ControlAsistencia.ReadModels.Sedes;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;

// Lookup cross-sede: rechazo barato antes de cargar el aggregate destino. Es puerto, y no
// IDocumentStore directo como en las Function GET, porque la rama cross-sede necesita cobertura
// unitaria con un fake manual y el harness de comando no fakea Marten.
public interface ILectorUbicacionDispositivo
{
    Task<UbicacionDispositivo?> BuscarUbicacionAsync(string dispositivoId, CancellationToken ct = default);
}
