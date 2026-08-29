using Bitakora.ControlAsistencia.ReadModels.Sedes;

namespace Bitakora.ControlAsistencia.Sedes.ResolverSedeDeMarcacionCuandoRegistroDeMarcacionCreado;

// Issue #467: abstrae los dos lookups de solo lectura contra el read-side propio de Sedes que
// MEF-ADR-0046 paso 2 exige (UbicacionDispositivo por DispositivoId, luego FichaSede por SedeId).
//
// Deliberadamente NO se inyecta IDocumentStore/ITenantResolver directo en el EventHandler (que si
// es el patron de las Function GET de este dominio -- ObtenerFichaSede, ListarFichasSede): esas
// Functions son un LoadAsync sin ramificacion propia, y sus tests dejan ese camino como caja negra
// del smoke test (ver ObtenerFichaColaborador/FunctionEndpointTests.cs, mismo precedente). Esta
// reaccion en cambio tiene logica de negocio propia no trivial (dos lookups encadenados, mapeo a
// SedeDeMarcacionResuelta, decision condicional de warning) que merece cobertura unitaria real con
// un fake manual -- nunca mockeando Marten en si (NSubstitute de IDocumentStore no aportaria
// cobertura real). La implementacion real (Infraestructura, Marten via QuerySession acotada a
// tenant) es responsabilidad del implementer.
public interface ILectorSedesParaMarcacion
{
    Task<UbicacionDispositivo?> BuscarUbicacionAsync(string dispositivoId, CancellationToken ct = default);

    Task<FichaSede?> BuscarFichaSedeAsync(string sedeId, CancellationToken ct = default);
}
