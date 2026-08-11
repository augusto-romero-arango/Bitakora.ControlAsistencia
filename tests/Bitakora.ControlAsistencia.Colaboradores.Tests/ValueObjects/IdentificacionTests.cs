// HU-348: Value object Identificacion -- compone la clave del stream de Colaborador.
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

/// <summary>
/// Interfaz publica: Crear(tipo, numero), Tipo, Numero, ToString() -- contrato de clave de stream.
/// </summary>
public class IdentificacionTests
{
    // ---------- CA-1: normalizacion trim + MAYUSCULAS garantiza el mismo stream ----------

    [Fact]
    public void Crear_NormalizaNumeroATrimYMayusculas_CuandoNumeroTraeEspaciosYMinusculas()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.CC, " ab-123 ");

        identificacion.ToString().Should().Be("CC:AB-123");
    }

    [Fact]
    public void ToString_ComponeTipoYNumeroConDosPuntos_CuandoIdentificacionValida()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.CE, "1098765432");

        identificacion.ToString().Should().Be("CE:1098765432");
    }

    // ---------- CA-2: numero alfanumerico aceptado (pasaportes traen letras) ----------

    [Fact]
    public void Crear_AceptaNumeroAlfanumerico_CuandoEsPasaporte()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.PA, "AB1234567");

        identificacion.Numero.Should().Be("AB1234567");
        identificacion.Tipo.Should().BeSameAs(TipoIdentificacion.PA);
    }

    // ---------- CA-2: numero vacio o whitespace rechazado con mensaje .resx ----------

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

    // ---------- Tipo/Numero: observables de solo lectura (identidad misma, no dato intermedio) ----------

    [Fact]
    public void Tipo_ExponeElTipoDeIdentificacion_CuandoIdentificacionCreada()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.TI, "12345");

        identificacion.Tipo.Should().BeSameAs(TipoIdentificacion.TI);
    }

    [Fact]
    public void Numero_ExponeElNumeroNormalizado_CuandoIdentificacionCreada()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.CC, " ab-123 ");

        identificacion.Numero.Should().Be("AB-123");
    }
}
