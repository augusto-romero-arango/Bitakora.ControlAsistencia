// Congela la premisa de que la igualdad del record basta (todo es string): si gana una coleccion o un
// Equals custom incompleto, la compararia por referencia y este test se pone rojo.

using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.Eventos;

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
