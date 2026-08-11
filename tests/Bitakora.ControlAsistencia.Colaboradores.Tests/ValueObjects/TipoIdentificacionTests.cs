// HU-348: Value object de lista cerrada TipoIdentificacion (Resolucion 2388/2016, PILA).
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

/// <summary>
/// TipoIdentificacion es una lista cerrada de instancias estaticas (CC, CE, TI, PA, PT) -- NUNCA
/// un enum C#: lo persistido y lo que compone claves de stream es siempre el codigo literal.
/// Interfaz publica: instancias estaticas CC/CE/TI/PA/PT, Desde(codigo), ToString().
/// </summary>
public class TipoIdentificacionTests
{
    // ---------- CA-2: Desde() rehidrata desde el codigo persistido ----------

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

    // Desde() es el boundary de rehidratacion desde el payload persistido: un "tipo": null en el
    // JSON debe fallar con el mensaje de dominio, no con el ArgumentNullException que el
    // diccionario lanzaria por su cuenta (que no dice nada del contrato roto).
    [Fact]
    public void Desde_LanzaArgumentException_CuandoCodigoEsNull()
    {
        var act = () => TipoIdentificacion.Desde(null!);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{TipoIdentificacion.Mensajes.CodigoNoReconocido}*");
    }

    // El codigo canonico es el unico aceptado: tolerar "cc" abriria dos representaciones para la
    // misma identidad y, con ellas, dos claves de stream distintas para el mismo colaborador.
    [Fact]
    public void Desde_LanzaArgumentException_CuandoCodigoVieneEnMinusculas()
    {
        var act = () => TipoIdentificacion.Desde("cc");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{TipoIdentificacion.Mensajes.CodigoNoReconocido}*");
    }

    // ---------- ToString(): el codigo literal, contrato de persistencia y de clave de stream ----------

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
