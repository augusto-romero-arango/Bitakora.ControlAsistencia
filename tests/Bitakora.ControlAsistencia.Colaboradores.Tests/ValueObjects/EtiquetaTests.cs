using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

public class EtiquetaTests
{
    [Fact]
    public void Crear_ProduceFormaOriginalYNormalizada_CuandoCategoriaYValorTienenTildesMayusculasYEspaciosExtremos()
    {
        var etiqueta = Etiqueta.Crear(" Área ", " Tecnología ");

        etiqueta.Categoria.Should().Be("Área");
        etiqueta.Valor.Should().Be("Tecnología");
        etiqueta.CategoriaNormalizada.Should().Be("area");
        etiqueta.ValorNormalizado.Should().Be("tecnologia");
    }

    [Fact]
    public void Crear_ConservaEspaciosInternos_CuandoValorTieneVariasPalabras()
    {
        var etiqueta = Etiqueta.Crear("Area", "  Recursos Humanos  ");

        etiqueta.Valor.Should().Be("Recursos Humanos");
        etiqueta.ValorNormalizado.Should().Be("recursos humanos");
    }

    // La enie se normaliza a "n" (FormD + quitar NonSpacingMark): "Diseno" y "Diseño" son la misma
    // etiqueta, decision deliberada para no fragmentar reportes. Si el dominio necesitara preservar la
    // enie, este test debe fallar primero.
    [Fact]
    public void Crear_NormalizaLaEnieComoN_CuandoValorLaContiene()
    {
        var conEnie = Etiqueta.Crear("Area", "Diseño");
        var sinEnie = Etiqueta.Crear("Area", "Diseno");

        conEnie.Valor.Should().Be("Diseño", "la forma original preserva la enie tal como se escribio");
        conEnie.ValorNormalizado.Should().Be("diseno");
        conEnie.Should().Be(sinEnie);
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoCategoriaEsVacia()
    {
        var act = () => Etiqueta.Crear(string.Empty, "Tecnologia");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Etiqueta.Mensajes.CategoriaVacia}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoCategoriaEsWhitespace()
    {
        var act = () => Etiqueta.Crear("   ", "Tecnologia");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Etiqueta.Mensajes.CategoriaVacia}*");
    }

    // El boundary recibe datos de fuera del dominio donde el null sobrevive al compilador: el rechazo
    // debe ser el mismo ArgumentException con mensaje .resx, no una NullReferenceException.
    [Fact]
    public void Crear_LanzaArgumentException_CuandoCategoriaEsNull()
    {
        var act = () => Etiqueta.Crear(null!, "Tecnologia");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Etiqueta.Mensajes.CategoriaVacia}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoValorEsVacio()
    {
        var act = () => Etiqueta.Crear("Area", string.Empty);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Etiqueta.Mensajes.ValorVacio}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoValorEsWhitespace()
    {
        var act = () => Etiqueta.Crear("Area", "   ");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Etiqueta.Mensajes.ValorVacio}*");
    }

    [Fact]
    public void Crear_LanzaArgumentException_CuandoValorEsNull()
    {
        var act = () => Etiqueta.Crear("Area", null!);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Etiqueta.Mensajes.ValorVacio}*");
    }

    [Fact]
    public void ToString_ComponeCategoriaYValorConDosPuntosUsandoFormasOriginales_CuandoEtiquetaValida()
    {
        var etiqueta = Etiqueta.Crear("Área", "Tecnología");

        etiqueta.ToString().Should().Be("Área:Tecnología");
    }

    [Fact]
    public void EsMismaCategoria_RetornaTrue_CuandoCategoriasNormalizadasCoincidenConValoresDistintos()
    {
        var ventas = Etiqueta.Crear("Área", "Ventas");
        var tecnologia = Etiqueta.Crear("area", "Tecnología");

        ventas.EsMismaCategoria(tecnologia).Should().BeTrue();
    }

    [Fact]
    public void EsMismaCategoria_RetornaFalse_CuandoCategoriasSonDistintas()
    {
        var sede = Etiqueta.Crear("Sede", "X");
        var area = Etiqueta.Crear("Area", "X");

        sede.EsMismaCategoria(area).Should().BeFalse();
    }

    // RetirarEtiqueta solo trae la categoria y Crear exige un valor: por eso el VO expone
    // NormalizarCategoria, y esa normalizacion sigue siendo suya, nunca del handler ni del aggregate.
    [Fact]
    public void NormalizarCategoria_RetornaFormaNormalizada_CuandoCategoriaTieneTildesMayusculasYEspaciosExtremos()
    {
        Etiqueta.NormalizarCategoria(" Área ").Should().Be("area");
    }

    [Fact]
    public void NormalizarCategoria_LanzaArgumentException_CuandoCategoriaEsVacia()
    {
        var act = () => Etiqueta.NormalizarCategoria(string.Empty);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Etiqueta.Mensajes.CategoriaVacia}*");
    }

    [Fact]
    public void NormalizarCategoria_LanzaArgumentException_CuandoCategoriaEsWhitespace()
    {
        var act = () => Etiqueta.NormalizarCategoria("   ");

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Etiqueta.Mensajes.CategoriaVacia}*");
    }

    [Fact]
    public void NormalizarCategoria_LanzaArgumentException_CuandoCategoriaEsNull()
    {
        var act = () => Etiqueta.NormalizarCategoria(null!);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{Etiqueta.Mensajes.CategoriaVacia}*");
    }
}
