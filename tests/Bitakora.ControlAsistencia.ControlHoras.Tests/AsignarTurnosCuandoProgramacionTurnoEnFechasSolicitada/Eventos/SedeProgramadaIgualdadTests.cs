// CrearInstancia/Copia pueblan todos los campos, incluidos los opcionales: con null en ambos lados un
// bug de igualdad sobre ese campo pasaria inadvertido.

using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.Eventos;

public class SedeProgramadaIgualdadTests : IgualdadTestBase<SedeProgramada>
{
    protected override SedeProgramada CrearInstancia() =>
        new("SEDE-SUBA", "Suba", "CC-100");

    protected override SedeProgramada CrearInstanciaCopia() =>
        new("SEDE-SUBA", "Suba", "CC-100");

    protected override IEnumerable<(string, SedeProgramada)> CrearInstanciasDiferentes()
    {
        yield return ("Id", new SedeProgramada("SEDE-CHAPINERO", "Suba", "CC-100"));
        yield return ("Nombre", new SedeProgramada("SEDE-SUBA", "Chapinero", "CC-100"));
        yield return ("CentroDeCostos", new SedeProgramada("SEDE-SUBA", "Suba", "CC-200"));
    }
}
