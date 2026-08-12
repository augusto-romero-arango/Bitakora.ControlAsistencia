// Issue #355. Requerido por regla 16: todo evento persistido en Marten debe sobrevivir
// Serialize -> Deserialize con las opciones REALES de Marten del dominio (regla 6d), nunca un
// resolver armado inline. CA-1.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarEtiquetaFunction.Eventos;

/// <summary>
/// Verifica que EtiquetaAsignada (payload rico: Etiqueta, VO de #353 con ctor privado) sobrevive
/// un roundtrip de serializacion STJ con las opciones reales de Marten del dominio -- y que NO
/// sobrevive sin el registro de ese VO en el resolver.
/// </summary>
public class EtiquetaAsignadaSerializacionTests
{
    private static readonly Etiqueta EtiquetaValida = Etiqueta.Crear("Área", "Tecnología");

    // Usa las opciones REALES de Marten del dominio (regla 6d) -- no un resolver armado inline que
    // solo registre este tipo. Esta eleccion es lo que convierte el RoundTrip de abajo en la
    // barrera contra el olvido de registrar Etiqueta dentro de
    // ConfiguracionSerializacionColaboradores.ConfigurarResolver (fase verde, issue #355).
    private static JsonSerializerOptions CrearOpcionesMarten() =>
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // CA-1: round-trip con datos completos, incluida la doble forma de categoria y valor.
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

    // Documenta POR QUE el registro en ConfigurarResolver es obligatorio: sin el, STJ no puede
    // invocar el ctor privado de Etiqueta. Quien detecta el olvido es el RoundTrip_* de arriba
    // (usa las opciones reales del dominio); este test fija el comportamiento del canal sin
    // resolver.
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
