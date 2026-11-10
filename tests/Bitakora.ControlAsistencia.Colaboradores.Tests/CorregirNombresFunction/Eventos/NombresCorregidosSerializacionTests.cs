using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.CorregirNombresFunction.Eventos;

public class NombresCorregidosSerializacionTests
{
    private static readonly NombreColaborador NombreValido =
        NombreColaborador.Crear("Luis", "Alberto", "Barreto", "Prieto");

    // Las opciones reales del dominio, no un resolver inline: asi el RoundTrip detecta que se olvide
    // registrar NombreColaborador en ConfigurarResolver.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new NombresCorregidos(NombreValido);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<NombresCorregidos>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Nombre.Should().Be(NombreValido);
        deserializado.Nombre.NombreCompleto.Should().Be("Luis Alberto Barreto Prieto");
    }

    [Fact]
    public void RoundTrip_ReconstruyeEvento_CuandoOpcionalesDelNombreSonAusentes()
    {
        var nombreSinOpcionales = NombreColaborador.Crear("Ana", null, "Gomez", null);
        var evento = new NombresCorregidos(nombreSinOpcionales);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<NombresCorregidos>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Nombre.Should().Be(nombreSinOpcionales);
        deserializado.Nombre.NombreCompleto.Should().Be("Ana Gomez");
    }

    // Fija por que NombreColaborador debe registrarse en ConfigurarResolver: sin el, STJ no invoca su
    // ctor privado.
    [Fact]
    public void Deserializar_LanzaNotSupportedException_CuandoElResolverNoRegistraElVoAnidado()
    {
        var evento = new NombresCorregidos(NombreValido);
        var opcionesSinRegistro = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            PropertyNamingPolicy = null
        };
        var json = JsonSerializer.Serialize(evento, opcionesSinRegistro);

        var act = () => JsonSerializer.Deserialize<NombresCorregidos>(json, opcionesSinRegistro);

        act.Should().Throw<NotSupportedException>();
    }
}
