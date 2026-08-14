// Issue #330. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (regla 6d), nunca un
// resolver armado inline. CA-6.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.RegistrarColaboradorFunction.Eventos;

/// <summary>
/// Verifica que ColaboradorRegistrado (payload rico: Identificacion + NombreColaborador, VOs de
/// #348 con ctor privado) sobrevive un roundtrip de serializacion STJ con las opciones reales de
/// Marten del dominio -- y que NO sobrevive sin el registro de esos VOs en el resolver.
/// </summary>
public class ColaboradorRegistradoSerializacionTests
{
    private static readonly Identificacion IdentificacionValida =
        Identificacion.Crear(TipoIdentificacion.CC, "79543210");

    private static readonly NombreColaborador NombreValido =
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", "Prieto");

    // Usa las opciones REALES de Marten del dominio (regla 6d) -- no un resolver armado inline que
    // solo registre este tipo. Esta eleccion es lo que convierte el RoundTrip de abajo en la
    // barrera contra el olvido de registrar Identificacion/NombreColaborador dentro de
    // ConfiguracionSerializacionColaboradores.ConfigurarResolver.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // CA-6: round-trip con datos completos (ambos opcionales presentes).
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

    // CA-6: round-trip con los opcionales ausentes (segundo nombre/apellido null).
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

    // Documenta POR QUE el registro en ConfigurarResolver es obligatorio: sin el, STJ no puede
    // invocar el ctor privado de Identificacion/NombreColaborador. Quien detecta el olvido son los
    // RoundTrip_* de arriba (usan las opciones reales del dominio); este test fija el
    // comportamiento del canal sin resolver.
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
