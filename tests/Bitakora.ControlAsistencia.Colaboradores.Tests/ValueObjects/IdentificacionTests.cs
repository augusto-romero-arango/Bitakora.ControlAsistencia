using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

public class IdentificacionTests
{
    [Fact]
    public void ToString_ComponeTipoYNumeroConGuion_CuandoIdentificacionValida()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.CC, "79543210");

        identificacion.ToString().Should().Be("CC-79543210");
    }

    [Fact]
    public void Crear_LimpiaElNumero_CuandoTraeEspaciosGuionesYPuntos()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.CC, " ab-12.3 ");

        identificacion.Numero.Should().Be("AB123");
    }

    [Fact]
    public void Crear_ConvierteLetrasAMayusculas_CuandoNumeroLlegaEnMinusculas()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.PA, "ab1234567");

        identificacion.Numero.Should().Be("AB1234567");
    }

    [Fact]
    public void Crear_EliminaEspaciosInternos_CuandoNumeroLosTraeEntreDigitos()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.CC, "795 432 10");

        identificacion.Numero.Should().Be("79543210");
    }

    // Alfanumerico ASCII: una letra acentuada o una enie se elimina; una implementacion con
    // char.IsLetterOrDigit (que acepta no-ASCII) debe fallar aqui.
    [Fact]
    public void Crear_EliminaLetrasAcentuadasYEnies_CuandoNumeroTraeCaracteresNoAscii()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.PA, "AÑ123É");

        identificacion.Numero.Should().Be("A123");
    }

    [Fact]
    public void Crear_ComponeLaMismaLlave_CuandoNumerosDifierenSoloPorCaracteresInvalidos()
    {
        var conGuion = Identificacion.Crear(TipoIdentificacion.CC, "AB-123");
        var sinGuion = Identificacion.Crear(TipoIdentificacion.CC, "ab123");

        conGuion.ToString().Should().Be("CC-AB123");
        sinGuion.ToString().Should().Be("CC-AB123");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoNumeroEsVacio()
    {
        var act = () => Identificacion.Crear(TipoIdentificacion.CC, string.Empty);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Identificacion.Mensajes.NumeroVacio}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoNumeroEsWhitespace()
    {
        var act = () => Identificacion.Crear(TipoIdentificacion.CC, "   ");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Identificacion.Mensajes.NumeroVacio}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoNumeroSoloTraeGuiones()
    {
        var act = () => Identificacion.Crear(TipoIdentificacion.CC, "---");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Identificacion.Mensajes.NumeroVacio}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoNumeroSoloTraePuntos()
    {
        var act = () => Identificacion.Crear(TipoIdentificacion.CC, "..");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Identificacion.Mensajes.NumeroVacio}*");
    }

    // STJ no honra la nulabilidad al deserializar: un numero null llega como null real y debe
    // terminar en ArgumentException, no en NullReferenceException.
    [Fact]
    public void Crear_LanzaArgumentException_CuandoNumeroEsNull()
    {
        var act = () => Identificacion.Crear(TipoIdentificacion.CC, null!);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Identificacion.Mensajes.NumeroVacio}*");
    }

    [Fact]
    public void Crear_AceptaNumeroAlfanumerico_CuandoEsPasaporte()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.PA, "AB1234567");

        identificacion.Numero.Should().Be("AB1234567");
        identificacion.Tipo.Should().BeSameAs(TipoIdentificacion.PA);
    }

    [Fact]
    public void Tipo_ExponeElTipoDeIdentificacion_CuandoIdentificacionCreada()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.TI, "12345");

        identificacion.Tipo.Should().BeSameAs(TipoIdentificacion.TI);
    }

    [Fact]
    public void Numero_ExponeElNumeroYaLimpio_CuandoIdentificacionCreada()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.CC, " ab-123 ");

        identificacion.Numero.Should().Be("AB123");
    }

    [Fact]
    public void Parsear_ReconstruyeLaIdentificacion_CuandoElValorTieneFormatoValido()
    {
        var identificacion = Identificacion.Parsear("CC-79543210");

        identificacion.Tipo.Should().BeSameAs(TipoIdentificacion.CC);
        identificacion.Numero.Should().Be("79543210");
    }

    [Fact]
    public void Parsear_CumpleRoundTrip_ConElToStringDeLaMismaIdentificacion()
    {
        var original = Identificacion.Crear(TipoIdentificacion.PA, "AB1234567");

        var reconstruida = Identificacion.Parsear(original.ToString());

        reconstruida.Should().Be(original);
    }

    [Fact]
    public void Parsear_LanzaArgumentException_CuandoElValorNoTraeGuion()
    {
        var act = () => Identificacion.Parsear("CC79543210");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Identificacion.Mensajes.FormatoInvalido}*");
    }

    [Fact]
    public void Parsear_LanzaArgumentException_CuandoElValorEsVacio()
    {
        var act = () => Identificacion.Parsear(string.Empty);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Identificacion.Mensajes.FormatoInvalido}*");
    }

    [Fact]
    public void Parsear_LanzaArgumentException_CuandoElValorEsNull()
    {
        var act = () => Identificacion.Parsear(null!);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Identificacion.Mensajes.FormatoInvalido}*");
    }

    [Fact]
    public void Parsear_LanzaArgumentException_CuandoElTipoNoEstaEnLaListaCerrada()
    {
        var act = () => Identificacion.Parsear("XX-79543210");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{TipoIdentificacion.Mensajes.CodigoNoReconocido}*");
    }

    [Fact]
    public void Parsear_LanzaArgumentException_CuandoElNumeroQuedaVacioTrasElGuion()
    {
        var act = () => Identificacion.Parsear("CC-");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Identificacion.Mensajes.NumeroVacio}*");
    }

    [Fact]
    public void Parsear_ReconstruyeLaIdentificacion_CuandoElTipoLlegaEnMinusculas()
    {
        var identificacion = Identificacion.Parsear("cc-79543210");

        identificacion.Tipo.Should().BeSameAs(TipoIdentificacion.CC);
        identificacion.Numero.Should().Be("79543210");
    }
}
