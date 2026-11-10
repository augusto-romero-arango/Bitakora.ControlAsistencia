using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaboradorFunction.Eventos;

public class ColaboradorRegistradoSerializacionTests
{
    private static readonly Identificacion IdentificacionValida =
        Identificacion.Crear(TipoIdentificacion.CC, "79543210");

    private static readonly NombreColaborador NombreValido =
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Prieto");

    // Las opciones reales del dominio, no un resolver inline: asi el RoundTrip detecta que se olvide
    // registrar Identificacion/NombreColaborador en ConfigurarResolver.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new ColaboradorRegistrado(IdentificacionValida, NombreValido);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<ColaboradorRegistrado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Identificacion.Should().Be(IdentificacionValida);
        deserializado.Identificacion.ToString().Should().Be("CC-79543210");
        deserializado.Nombre.Should().Be(NombreValido);
        deserializado.Nombre.NombreCompleto.Should().Be("Luis Augusto Barreto Prieto");
    }

    [Fact]
    public void RoundTrip_ReconstruyeEvento_CuandoOpcionalesDelNombreSonAusentes()
    {
        var nombreSinOpcionales = NombreColaborador.Crear("Ana", null, "Gomez", null);
        var evento = new ColaboradorRegistrado(IdentificacionValida, nombreSinOpcionales);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<ColaboradorRegistrado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Nombre.Should().Be(nombreSinOpcionales);
        deserializado.Nombre.NombreCompleto.Should().Be("Ana Gomez");
    }

    // Fija por que Identificacion/NombreColaborador deben registrarse en ConfigurarResolver: sin el, STJ
    // no invoca su ctor privado.
    [Fact]
    public void Deserializar_LanzaNotSupportedException_CuandoElResolverNoRegistraLosVosAnidados()
    {
        var evento = new ColaboradorRegistrado(IdentificacionValida, NombreValido);
        var opcionesSinRegistro = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            PropertyNamingPolicy = null
        };
        var json = JsonSerializer.Serialize(evento, opcionesSinRegistro);

        var act = () => JsonSerializer.Deserialize<ColaboradorRegistrado>(json, opcionesSinRegistro);

        act.Should().Throw<NotSupportedException>();
    }
}
