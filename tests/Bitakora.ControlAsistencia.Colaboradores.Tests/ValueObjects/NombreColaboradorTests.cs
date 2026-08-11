// HU-348: Value object NombreColaborador -- dueno unico de la composicion del nombre completo.
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

/// <summary>
/// Interfaz publica: Crear(primerNombre, segundoNombre?, primerApellido, segundoApellido?),
/// NombreCompleto, ToString(). Los 4 componentes NO son publicos (Tell-don't-Ask, MEF-ADR-0012).
/// </summary>
public class NombreColaboradorTests
{
    // ---------- CA-3: primer nombre obligatorio ----------

    [Fact]
    public void Crear_LanzaArgumentException_CuandoPrimerNombreEsNull()
    {
        var act = () => NombreColaborador.Crear(null!, null, "Barreto", null);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{NombreColaborador.Mensajes.PrimerNombreRequerido}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoPrimerNombreEsVacio()
    {
        var act = () => NombreColaborador.Crear(string.Empty, null, "Barreto", null);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{NombreColaborador.Mensajes.PrimerNombreRequerido}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoPrimerNombreEsWhitespace()
    {
        var act = () => NombreColaborador.Crear("   ", null, "Barreto", null);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{NombreColaborador.Mensajes.PrimerNombreRequerido}*");
    }

    // ---------- CA-3: primer apellido obligatorio ----------

    [Fact]
    public void Crear_LanzaArgumentException_CuandoPrimerApellidoEsNull()
    {
        var act = () => NombreColaborador.Crear("Luis", null, null!, null);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{NombreColaborador.Mensajes.PrimerApellidoRequerido}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoPrimerApellidoEsVacio()
    {
        var act = () => NombreColaborador.Crear("Luis", null, string.Empty, null);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{NombreColaborador.Mensajes.PrimerApellidoRequerido}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoPrimerApellidoEsWhitespace()
    {
        var act = () => NombreColaborador.Crear("Luis", null, "   ", null);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{NombreColaborador.Mensajes.PrimerApellidoRequerido}*");
    }

    // ---------- CA-4: NombreCompleto compone con espacios simples ----------

    [Fact]
    public void NombreCompleto_ComponeLos4Componentes_CuandoTodosPresentes()
    {
        var nombre = NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Gomez");

        nombre.NombreCompleto.Should().Be("Luis Augusto Barreto Gomez");
    }

    [Fact]
    public void NombreCompleto_OmiteSegundosAusentes_CuandoSonNull()
    {
        var nombre = NombreColaborador.Crear("Luis", null, "Barreto", null);

        nombre.NombreCompleto.Should().Be("Luis Barreto");
    }

    [Fact]
    public void NombreCompleto_OmiteSegundosAusentes_CuandoSonVaciosOWhitespace()
    {
        var nombre = NombreColaborador.Crear("Luis", "  ", "Barreto", string.Empty);

        nombre.NombreCompleto.Should().Be("Luis Barreto");
    }

    [Fact]
    public void NombreCompleto_ComponeSoloSegundoNombre_CuandoSegundoApellidoAusente()
    {
        var nombre = NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

        nombre.NombreCompleto.Should().Be("Luis Augusto Barreto");
    }

    [Fact]
    public void NombreCompleto_ComponeSoloSegundoApellido_CuandoSegundoNombreAusente()
    {
        var nombre = NombreColaborador.Crear("Luis", null, "Barreto", "Gomez");

        nombre.NombreCompleto.Should().Be("Luis Barreto Gomez");
    }

    [Fact]
    public void Crear_NormalizaConTrim_CuandoComponentesTraenEspacios()
    {
        var nombre = NombreColaborador.Crear(" Luis ", " Augusto ", " Barreto ", " Gomez ");

        nombre.NombreCompleto.Should().Be("Luis Augusto Barreto Gomez");
    }

    [Fact]
    public void ToString_RetornaElNombreCompletoLiteral_CuandoNombreValido()
    {
        var nombre = NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Gomez");

        nombre.ToString().Should().Be("Luis Augusto Barreto Gomez");
    }

    // El issue declara ToString() como identico a NombreCompleto (contrato de diseno explicito,
    // no coincidencia derivada de la logica bajo prueba): ambos deben mostrar el mismo texto.
    [Fact]
    public void ToString_EsIgualANombreCompleto_CuandoNombreValido()
    {
        var nombre = NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Gomez");

        nombre.ToString().Should().Be(nombre.NombreCompleto);
    }
}
