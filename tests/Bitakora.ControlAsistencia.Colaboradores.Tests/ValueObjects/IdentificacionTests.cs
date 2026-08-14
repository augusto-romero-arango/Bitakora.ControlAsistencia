// HU-348: Value object Identificacion -- compone la clave del stream de Colaborador.
// Issue #381: separador de la llave cambia de ":" a "-"; el numero se limpia a alfanumerico ASCII
// (letras a MAYUSCULAS, cualquier otro caracter -- incluidas letras acentuadas/enie -- se ELIMINA
// silenciosamente). El trim de #348 queda SUBSUMIDO por la limpieza: los espacios son caracteres
// invalidos y se eliminan igual que un guion o un punto.
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

/// <summary>
/// Interfaz publica: Crear(tipo, numero), Tipo, Numero, ToString() -- contrato de clave de stream.
/// </summary>
public class IdentificacionTests
{
    // ---------- CA-1: ToString() compone la llave con guion ----------

    [Fact]
    public void ToString_ComponeTipoYNumeroConGuion_CuandoIdentificacionValida()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.CC, "79543210");

        identificacion.ToString().Should().Be("CC-79543210");
    }

    // ---------- CA-2: limpieza del numero (letras a MAYUSCULAS, no-alfanumerico eliminado) ----------

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

    // Desviacion documentada respecto a la propuesta del planner: el issue deja explicito que el
    // alcance de "letras" es alfanumerico ASCII ([A-Z0-9]) -- una letra acentuada o una enie es
    // caracter invalido y se elimina igual que un guion, no se conserva como letra valida.
    [Fact]
    public void Crear_EliminaLetrasAcentuadasYEnies_CuandoNumeroTraeCaracteresNoAscii()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.PA, "AÑ123É");

        identificacion.Numero.Should().Be("A123");
    }

    // ---------- CA-3: la limpieza unifica la identidad (ToString / llave del stream) ----------

    [Fact]
    public void Crear_ComponeLaMismaLlave_CuandoNumerosDifierenSoloPorCaracteresInvalidos()
    {
        var conGuion = Identificacion.Crear(TipoIdentificacion.CC, "AB-123");
        var sinGuion = Identificacion.Crear(TipoIdentificacion.CC, "ab123");

        conGuion.ToString().Should().Be("CC-AB123");
        sinGuion.ToString().Should().Be("CC-AB123");
    }

    // ---------- CA-4: numero que queda vacio tras la limpieza lanza ArgumentException ----------

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

    // ---------- Numero alfanumerico sin caracteres a limpiar (pasaportes traen letras) ----------

    [Fact]
    public void Crear_AceptaNumeroAlfanumerico_CuandoEsPasaporte()
    {
        var identificacion = Identificacion.Crear(TipoIdentificacion.PA, "AB1234567");

        identificacion.Numero.Should().Be("AB1234567");
        identificacion.Tipo.Should().BeSameAs(TipoIdentificacion.PA);
    }

    // ---------- Tipo/Numero: observables de solo lectura (identidad misma, no dato intermedio) ----------

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
}
