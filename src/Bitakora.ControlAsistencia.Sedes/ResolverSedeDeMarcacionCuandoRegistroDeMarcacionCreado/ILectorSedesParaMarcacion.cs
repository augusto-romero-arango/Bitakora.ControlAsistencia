using Bitakora.ControlAsistencia.ReadModels.Sedes;

namespace Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;

// Puerto de los dos lookups contra el read-side propio (MEF-ADR-0046 paso 2).
//
// No inyectar IDocumentStore/ITenantResolver directo en el EventHandler, aunque ese SI sea el
// patron de las Function GET de este dominio: aquellas son un LoadAsync sin ramificacion y dejan
// Marten como caja negra del smoke test. Esta reaccion tiene logica propia ramificada (dos lookups
// encadenados con cortocircuito, mapeo del evento, decision de warning) que necesita cobertura
// unitaria con un fake manual -- mockear Marten no la daria.
public interface ILectorSedesParaMarcacion
{
    Task<UbicacionDispositivo?> BuscarUbicacionAsync(string dispositivoId, CancellationToken ct = default);

    Task<FichaSede?> BuscarFichaSedeAsync(string sedeId, CancellationToken ct = default);
}
