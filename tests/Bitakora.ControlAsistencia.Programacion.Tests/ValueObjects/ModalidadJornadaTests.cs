using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class ModalidadJornadaTests
{
    [Fact]
    public void Desde_RetornaLaInstanciaCanonica_CuandoElNombreCoincideExacto()
    {
        var modalidad = ModalidadJornada.Desde("TurnosSucesivos");

        modalidad.Should().BeSameAs(ModalidadJornada.TurnosSucesivos);
        modalidad.Nombre.Should().Be("TurnosSucesivos");
    }

    [Fact]
    public void Desde_RetornaLaInstanciaCanonica_CuandoElNombreVieneEnMinusculas()
    {
        ModalidadJornada.Desde("ordinaria").Should().BeSameAs(ModalidadJornada.Ordinaria);
    }

    [Fact]
    public void TryDesde_RetornaFalse_CuandoElNombreNoEstaEnElCatalogo()
    {
        var reconocida = ModalidadJornada.TryDesde("Teletrabajo", out var modalidad);

        reconocida.Should().BeFalse();
        modalidad.Should().BeNull();
    }

    [Fact]
    public void TryDesde_RetornaFalse_CuandoElNombreEsNulo()
    {
        ModalidadJornada.TryDesde(null, out _).Should().BeFalse();
    }

    [Fact]
    public void Desde_LanzaArgumentException_CuandoElNombreNoEstaEnElCatalogo()
    {
        var act = () => ModalidadJornada.Desde("Teletrabajo");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{ModalidadJornada.Mensajes.NombreNoReconocido}*");
    }

    [Fact]
    public void ToString_RetornaElNombreLegible_CuandoEsOrdinariaOFlexible()
    {
        ModalidadJornada.Ordinaria.ToString().Should().Be("Ordinaria");
        ModalidadJornada.Flexible.ToString().Should().Be("Flexible");
    }

    [Fact]
    public void ToString_RetornaElNombreLegibleConTildes_CuandoEsTurnosSucesivosSinSolucionOExcluida()
    {
        ModalidadJornada.TurnosSucesivos.ToString().Should().Be("Turnos sucesivos");
        ModalidadJornada.SinSolucionDeContinuidad.ToString().Should().Be("Sin solución de continuidad");
        ModalidadJornada.ExcluidaDeJornadaMaxima.ToString().Should().Be("Excluida de la jornada máxima");
    }

    [Fact]
    public void Equals_EsVerdadero_CuandoLosNombresCoinciden()
    {
        ModalidadJornada.Desde("Flexible").Equals(ModalidadJornada.Flexible).Should().BeTrue();
        ModalidadJornada.Flexible.Equals(ModalidadJornada.Ordinaria).Should().BeFalse();
    }
}
