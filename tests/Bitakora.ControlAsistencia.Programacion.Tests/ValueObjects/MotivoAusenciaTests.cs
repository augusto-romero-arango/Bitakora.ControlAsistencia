using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class MotivoAusenciaTests
{
    [Fact]
    public void Desde_RetornaLaInstanciaCanonica_CuandoElNombreCoincideExacto()
    {
        var motivo = MotivoAusencia.Desde("IncapacidadMedica");

        motivo.Should().BeSameAs(MotivoAusencia.IncapacidadMedica);
        motivo.ToString().Should().Be("IncapacidadMedica");
    }

    [Fact]
    public void Desde_RetornaLaInstanciaCanonica_CuandoElNombreVieneEnMinusculas()
    {
        var motivo = MotivoAusencia.Desde("vacaciones");

        motivo.Should().BeSameAs(MotivoAusencia.Vacaciones);
        motivo.ToString().Should().Be("Vacaciones");
    }

    [Fact]
    public void Desde_LanzaArgumentException_CuandoElNombreNoEstaEnLaLista()
    {
        var act = () => MotivoAusencia.Desde("Teletrabajo");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{MotivoAusencia.Mensajes.NombreNoReconocido}*");
    }
}
