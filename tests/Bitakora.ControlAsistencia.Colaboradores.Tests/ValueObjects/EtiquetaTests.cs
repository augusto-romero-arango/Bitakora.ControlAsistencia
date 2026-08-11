// HU-353: Value object Etiqueta -- par categoria:valor libre con doble forma (original y
// normalizada) por campo. Este corte solo entrega el VO; la operacion de negocio
// (asignar/retirar con salvaguarda de unicidad por categoria) es el issue #355.
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

/// <summary>
/// Interfaz publica: Crear(categoria, valor), Categoria, Valor, CategoriaNormalizada,
/// ValorNormalizado, EsMismaCategoria(otra), ToString().
/// </summary>
public class EtiquetaTests
{
    // ---------- CA-1: doble forma por campo (original con trim + normalizada sin tildes/mayus) ----------

    [Fact]
    public void Crear_ProduceFormaOriginalYNormalizada_CuandoCategoriaYValorTienenTildesMayusculasYEspaciosExtremos()
    {
        var etiqueta = Etiqueta.Crear(" Área ", " Tecnología ");

        etiqueta.Categoria.Should().Be("Área");
        etiqueta.Valor.Should().Be("Tecnología");
        etiqueta.CategoriaNormalizada.Should().Be("area");
        etiqueta.ValorNormalizado.Should().Be("tecnologia");
    }

    // ---------- CA-5: espacios internos se conservan (solo se recorta en los extremos) ----------

    [Fact]
    public void Crear_ConservaEspaciosInternos_CuandoValorTieneVariasPalabras()
    {
        var etiqueta = Etiqueta.Crear("Area", "  Recursos Humanos  ");

        etiqueta.Valor.Should().Be("Recursos Humanos");
        etiqueta.ValorNormalizado.Should().Be("recursos humanos");
    }

    // La enie NO es un acento: es una letra propia del espanol. El patron de normalizacion que el
    // issue prescribe (FormD + remover NonSpacingMark) la descompone en "n" + tilde combinante y la
    // colapsa a "n", asi que "Diseno" y "Diseño" son la MISMA etiqueta. Es consecuencia inevitable
    // del patron elegido, no un descuido: en etiquetas libres tecleadas por el cliente evita
    // fragmentar el reporte cuando alguien escribe sin enie. Este test lo fija como decision
    // observable en vez de dejarlo tacito -- si el dominio necesitara preservar la enie, este test
    // es el que debe fallar primero (agregado en revision, issue #353).
    [Fact]
    public void Crear_NormalizaLaEnieComoN_CuandoValorLaContiene()
    {
        var conEnie = Etiqueta.Crear("Area", "Diseño");
        var sinEnie = Etiqueta.Crear("Area", "Diseno");

        conEnie.Valor.Should().Be("Diseño", "la forma original preserva la enie tal como se escribio");
        conEnie.ValorNormalizado.Should().Be("diseno");
        conEnie.Should().Be(sinEnie);
    }

    // ---------- CA-2: categoria/valor vacios o whitespace -> rechazo con mensaje .resx ----------

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

    // Los parametros son string no anulable, pero el boundary recibe datos de fuera del dominio
    // (deserializacion, payload HTTP) donde el null sobrevive al compilador: el rechazo debe ser el
    // mismo ArgumentException con mensaje .resx, no una NullReferenceException opaca. Paridad con
    // NombreColaboradorTests (issue #348), que ya cubre null! en sus campos requeridos.
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

    // ---------- ToString: formas originales (display), no las normalizadas ----------

    [Fact]
    public void ToString_ComponeCategoriaYValorConDosPuntosUsandoFormasOriginales_CuandoEtiquetaValida()
    {
        var etiqueta = Etiqueta.Crear("Área", "Tecnología");

        etiqueta.ToString().Should().Be("Área:Tecnología");
    }

    // ---------- CA-4: EsMismaCategoria -- superficie para la salvaguarda de unicidad de #355 ----------

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
}
