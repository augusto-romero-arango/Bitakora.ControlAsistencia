using Bitakora.ControlAsistencia.ReadModels.Sedes;

namespace Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;

// Es puerto, y no IDocumentStore directo como en las Function GET, porque la reaccion tiene logica
// ramificada que necesita cobertura unitaria con un fake manual.
public interface ILectorSedesParaMarcacion
{
    Task<UbicacionDispositivo?> BuscarUbicacionAsync(string dispositivoId, CancellationToken ct = default);

    Task<FichaSede?> BuscarFichaSedeAsync(string sedeId, CancellationToken ct = default);
}
