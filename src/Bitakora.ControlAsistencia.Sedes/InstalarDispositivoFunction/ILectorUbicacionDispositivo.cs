using Bitakora.ControlAsistencia.ReadModels.Sedes;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;

// Puerto del unico lookup cross-sede que el comando necesita: rechazo barato antes de cargar el
// aggregate destino.
//
// No inyectar IDocumentStore/ITenantResolver directo en el CommandHandler, aunque ese SI sea el
// patron de las Function GET de este dominio: la rama cross-sede es logica ramificada que necesita
// cobertura unitaria con un fake manual, y el harness de comando no fakea Marten. Mismo criterio
// que ILectorSedesParaMarcacion.
public interface ILectorUbicacionDispositivo
{
    Task<UbicacionDispositivo?> BuscarUbicacionAsync(string dispositivoId, CancellationToken ct = default);
}
