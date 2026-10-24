using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;

public class NombreDeCatalogoTests
{
    [Fact]
    public void Normalizar_RecortaYColapsaEspaciosYPasaAMayusculas_CuandoTraeEspaciosSobrantes()
    {
        NombreDeCatalogo.Normalizar("  turno   manana ").Should().Be("TURNO MANANA");
    }

    [Fact]
    public void Normalizar_ConservaLosAcentos_CuandoElNombreLosTiene()
    {
        NombreDeCatalogo.Normalizar("Turno Mañana").Should().Be("TURNO MAÑANA");
        NombreDeCatalogo.Normalizar("Turno Mañana").Should().NotBe(NombreDeCatalogo.Normalizar("Turno Manana"));
    }
}
