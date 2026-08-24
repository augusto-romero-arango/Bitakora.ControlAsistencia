// Todos los campos de ResumenColaborador son string: la igualdad por valor del record por defecto ya
// es correcta y no necesita Equals/GetHashCode propios (MEF-ADR-0012), mismo criterio que el resto
// de los payloads planos del bus. Estos tests congelan esa premisa: si el record gana una coleccion, la
// compararia por referencia y el test rojo lo delata.

using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.Colaboradores;

public class ResumenColaboradorIgualdadTests : IgualdadTestBase<ResumenColaborador>
{
    protected override ResumenColaborador CrearInstancia() =>
        new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    protected override ResumenColaborador CrearInstanciaCopia() =>
        new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    protected override IEnumerable<(string, ResumenColaborador)> CrearInstanciasDiferentes()
    {
        yield return ("Identificacion",
            new ResumenColaborador("CC-9999999999", "EMP-001", "Luis Augusto Barreto"));
        yield return ("CodigoColaborador",
            new ResumenColaborador("CC-1234567890", "EMP-002", "Luis Augusto Barreto"));
        yield return ("NombreCompleto",
            new ResumenColaborador("CC-1234567890", "EMP-001", "Otro Nombre Completo"));
    }
}
