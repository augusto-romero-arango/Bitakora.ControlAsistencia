// HU-348 CA-6: round-trip de serializacion (patron Marten) para Identificacion.
// ConfiguracionSerializacionColaboradores todavia no existe -- este issue no introduce ningun
// evento persistido (ver "Investigacion del planner" del issue #348), asi que el resolver se
// arma localmente con Identificacion.ConfigurarSerializacion, igual que
// Programacion.Tests/ValueObjects/SubFranjaSerializacionTests.cs hace con SubFranja antes de que
// un evento persistido la reclame como payload.
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.ValueObjects;

public class IdentificacionSerializacionTests
{
    private static JsonSerializerOptions CrearOpciones()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        Identificacion.ConfigurarSerializacion(resolver);
        return new JsonSerializerOptions { TypeInfoResolver = resolver, PropertyNamingPolicy = null };
    }

    [Fact]
    public void RoundTrip_PreservaIgualdad_CuandoIdentificacionValida()
    {
        var original = Identificacion.Crear(TipoIdentificacion.CC, "1098765432");
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Identificacion>(json, opciones);

        restaurado.Should().Be(original);
    }

    [Fact]
    public void RoundTrip_PreservaTipoYNumero_CuandoIdentificacionEsPasaporte()
    {
        var original = Identificacion.Crear(TipoIdentificacion.PA, "AB1234567");
        var opciones = CrearOpciones();

        var json = JsonSerializer.Serialize(original, opciones);
        var restaurado = JsonSerializer.Deserialize<Identificacion>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Tipo.Should().BeSameAs(TipoIdentificacion.PA);
        restaurado.Numero.Should().Be("AB1234567");
    }

    // Barrera anti-regresion: sin el resolver custom (equivalente a olvidar registrar el VO), STJ
    // no puede reconstruir Identificacion -- su unico constructor es privado y no hay
    // [JsonConstructor] (proscrito por MEF-ADR-0012).
    [Fact]
    public void Deserializar_Falla_CuandoResolverNoTieneRegistroDeIdentificacion()
    {
        var original = Identificacion.Crear(TipoIdentificacion.CC, "1098765432");
        var json = JsonSerializer.Serialize(original, CrearOpciones());
        var opciones = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        var act = () => JsonSerializer.Deserialize<Identificacion>(json, opciones);

        act.Should().Throw<NotSupportedException>();
    }
}
