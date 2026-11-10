using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarEtiquetaFunction.Eventos;

public class EtiquetaAsignadaSerializacionTests
{
    private static readonly Etiqueta EtiquetaValida = Etiqueta.Crear("Área", "Tecnología");

    // Las opciones reales del dominio, no un resolver inline: asi el RoundTrip detecta que se olvide
    // registrar Etiqueta en ConfigurarResolver.
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    [Fact]
    public void RoundTrip_ReconstruyeEvento_ConDatosCompletos()
    {
        var evento = new EtiquetaAsignada(EtiquetaValida);
        var opciones = CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<EtiquetaAsignada>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Etiqueta.Should().Be(EtiquetaValida);
        deserializado.Etiqueta.Categoria.Should().Be("Área");
        deserializado.Etiqueta.Valor.Should().Be("Tecnología");
        deserializado.Etiqueta.CategoriaNormalizada.Should().Be("area");
        deserializado.Etiqueta.ValorNormalizado.Should().Be("tecnologia");
    }

    // Fija por que Etiqueta debe registrarse en ConfigurarResolver: sin el, STJ no invoca su ctor privado.
    [Fact]
    public void Deserializar_LanzaNotSupportedException_CuandoElResolverNoRegistraElVoAnidado()
    {
        var evento = new EtiquetaAsignada(EtiquetaValida);
        var opcionesSinRegistro = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            PropertyNamingPolicy = null
        };
        var json = JsonSerializer.Serialize(evento, opcionesSinRegistro);

        var act = () => JsonSerializer.Deserialize<EtiquetaAsignada>(json, opcionesSinRegistro);

        act.Should().Throw<NotSupportedException>();
    }
}
