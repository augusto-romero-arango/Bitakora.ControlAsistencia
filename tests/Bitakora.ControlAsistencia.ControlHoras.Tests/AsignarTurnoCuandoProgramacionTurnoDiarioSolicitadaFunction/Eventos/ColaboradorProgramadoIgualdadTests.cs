// Todos los campos de ColaboradorProgramado son string, asi que la igualdad por valor del record ya
// es correcta sin Equals/GetHashCode custom. Este test congela esa premisa: el dia que el record gane
// una coleccion (o un Equals custom incompleto), la compararia por referencia y el rojo lo delata.

using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

public class ColaboradorProgramadoIgualdadTests : IgualdadTestBase<ColaboradorProgramado>
{
    protected override ColaboradorProgramado CrearInstancia() =>
        new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    protected override ColaboradorProgramado CrearInstanciaCopia() =>
        new("CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    protected override IEnumerable<(string, ColaboradorProgramado)> CrearInstanciasDiferentes()
    {
        yield return ("Identificacion",
            new ColaboradorProgramado("CE-1234567890", "EMP-001", "Luis Augusto Barreto"));
        yield return ("CodigoColaborador",
            new ColaboradorProgramado("CC-1234567890", "EMP-002", "Luis Augusto Barreto"));
        yield return ("NombreCompleto",
            new ColaboradorProgramado("CC-1234567890", "EMP-001", "Otro Nombre Barreto"));
    }
}
