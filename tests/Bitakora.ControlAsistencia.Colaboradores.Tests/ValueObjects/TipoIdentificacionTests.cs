using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

public class TipoIdentificacionTests
{
    [Fact]
    public void Desde_RetornaInstanciaCC_CuandoCodigoEsCC()
    {
        var resultado = TipoIdentificacion.Desde("CC");

        resultado.Should().BeSameAs(TipoIdentificacion.CC);
    }

    [Fact]
    public void Desde_RetornaInstanciaCE_CuandoCodigoEsCE()
    {
        var resultado = TipoIdentificacion.Desde("CE");

        resultado.Should().BeSameAs(TipoIdentificacion.CE);
    }

    [Fact]
    public void Desde_RetornaInstanciaTI_CuandoCodigoEsTI()
    {
        var resultado = TipoIdentificacion.Desde("TI");

        resultado.Should().BeSameAs(TipoIdentificacion.TI);
    }

    [Fact]
    public void Desde_RetornaInstanciaPA_CuandoCodigoEsPA()
    {
        var resultado = TipoIdentificacion.Desde("PA");

        resultado.Should().BeSameAs(TipoIdentificacion.PA);
    }

    [Fact]
    public void Desde_RetornaInstanciaPT_CuandoCodigoEsPT()
    {
        var resultado = TipoIdentificacion.Desde("PT");

        resultado.Should().BeSameAs(TipoIdentificacion.PT);
    }

    [Fact]
    public void Desde_LanzaArgumentException_CuandoCodigoFueraDeLaListaCerrada()
    {
        var act = () => TipoIdentificacion.Desde("XX");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{TipoIdentificacion.Mensajes.CodigoNoReconocido}*");
    }

    // Un null en el payload debe fallar con el mensaje de dominio, no con el ArgumentNullException
    // del diccionario.
    [Fact]
    public void Desde_LanzaArgumentException_CuandoCodigoEsNull()
    {
        var act = () => TipoIdentificacion.Desde(null!);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{TipoIdentificacion.Mensajes.CodigoNoReconocido}*");
    }

    [Fact]
    public void Desde_LanzaArgumentException_CuandoCodigoEsVacio()
    {
        var act = () => TipoIdentificacion.Desde(string.Empty);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{TipoIdentificacion.Mensajes.CodigoNoReconocido}*");
    }

    [Fact]
    public void Desde_LanzaArgumentException_CuandoCodigoEsSoloEspacios()
    {
        var act = () => TipoIdentificacion.Desde("   ");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{TipoIdentificacion.Mensajes.CodigoNoReconocido}*");
    }

    // Normalizar no abre dos claves de stream: Desde() siempre retorna la instancia canonica.
    [Fact]
    public void Desde_RetornaInstanciaCC_CuandoCodigoTieneEspaciosYMinusculas()
    {
        var resultado = TipoIdentificacion.Desde(" cc ");

        resultado.Should().BeSameAs(TipoIdentificacion.CC);
    }

    [Fact]
    public void Desde_RetornaInstanciaCC_CuandoCodigoEsMixedCase()
    {
        var resultado = TipoIdentificacion.Desde("Cc");

        resultado.Should().BeSameAs(TipoIdentificacion.CC);
    }

    [Fact]
    public void Desde_RetornaInstanciaPT_CuandoCodigoEstaEnMinusculas()
    {
        var resultado = TipoIdentificacion.Desde("pt");

        resultado.Should().BeSameAs(TipoIdentificacion.PT);
    }

    // ToString() es el codigo literal: contrato de persistencia y de la clave de stream.
    [Fact]
    public void ToString_RetornaCodigoLiteral_CuandoInstanciaEsCC()
    {
        TipoIdentificacion.CC.ToString().Should().Be("CC");
    }

    [Fact]
    public void ToString_RetornaCodigoLiteral_CuandoInstanciaEsPA()
    {
        TipoIdentificacion.PA.ToString().Should().Be("PA");
    }
}
