// Issue #183 CA-6: el payload completo de DiaCalculado round-trip serializa/deserializa con el
// serializador POR DEFECTO del publisher, SIN resolver custom y sin perdida.
// Cura de raiz del bug del smoke CA-5 (NullReferenceException, field notes 2026-06-23-1924): antes
// el payload llevaba VOs ricos (IntervaloTemporal, DetalleRetardo) que solo serializaban bien con el
// resolver custom de Marten, resolver que NO se aplica al canal de publicacion a Service Bus. Ahora
// el payload es 100% primitivo (HorasDiscriminadas), asi que NO se construye CrearOpcionesMarten():
// el test usa opciones por defecto a proposito, para demostrar que ningun resolver custom es necesario.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.Eventos;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;

public class DiaCalculadoSerializacionTests
{
    private static readonly InformacionEmpleado Empleado =
        new("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static HorasDiscriminadas CrearHorasConDatos() =>
        new(
            new Dictionary<string, int>
            {
                ["DominicalFestivaDiurna"] = 420,
                ["ExtraDiurnaDominicalFestiva"] = 60,
                ["Retardo"] = 30
            },
            []);

    // CA-6: roundtrip del payload con datos, usando el serializador POR DEFECTO (sin opciones).
    // Verifica que MinutosPorConcepto (incluida la clave "Retardo") sobrevive sin perdida.
    [Fact]
    public void RoundTrip_PreservaMinutosPorConcepto_ConSerializadorPorDefecto()
    {
        var original = new DiaCalculado(Empleado, Fecha, CrearHorasConDatos());

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<DiaCalculado>(json);

        restaurado.Should().NotBeNull();
        restaurado!.InformacionEmpleado.Should().Be(Empleado);
        restaurado.Fecha.Should().Be(Fecha);
        restaurado.HorasDiscriminadas.MinutosPorConcepto.Should().BeEquivalentTo(
            new Dictionary<string, int>
            {
                ["DominicalFestivaDiurna"] = 420,
                ["ExtraDiurnaDominicalFestiva"] = 60,
                ["Retardo"] = 30
            });
        restaurado.HorasDiscriminadas.Trazabilidad.Should().BeEmpty();
    }

    // CA-6: roundtrip con el formato del publisher (camelCase, case-insensitive), que mimetiza el
    // canal real de Service Bus. Las claves de concepto del diccionario NO se ven afectadas por
    // PropertyNamingPolicy y sobreviven intactas - es justo lo que rompia el bug del smoke CA-5.
    [Fact]
    public void RoundTrip_PreservaPayload_CuandoPublisherUsaCamelCase()
    {
        var publisherOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        var original = new DiaCalculado(Empleado, Fecha, CrearHorasConDatos());

        var json = JsonSerializer.Serialize(original, publisherOptions);
        var restaurado = JsonSerializer.Deserialize<DiaCalculado>(json, publisherOptions);

        restaurado.Should().NotBeNull();
        restaurado!.Fecha.Should().Be(Fecha);
        restaurado.InformacionEmpleado!.EmpleadoId.Should().Be(Empleado.EmpleadoId);
        restaurado.HorasDiscriminadas.MinutosPorConcepto["DominicalFestivaDiurna"].Should().Be(420);
        restaurado.HorasDiscriminadas.MinutosPorConcepto["Retardo"].Should().Be(30);
    }

    // CA-6: roundtrip con InformacionEmpleado null (ControlDiario nacido solo por marcacion) y
    // payload vacio (dia anomalo o sin turno) - el nullable y las colecciones vacias se preservan.
    [Fact]
    public void RoundTrip_PreservaCampos_CuandoInformacionEmpleadoEsNulaYPayloadVacio()
    {
        var original = new DiaCalculado(
            null,
            Fecha,
            new HorasDiscriminadas(new Dictionary<string, int>(), []));

        var json = JsonSerializer.Serialize(original);
        var restaurado = JsonSerializer.Deserialize<DiaCalculado>(json);

        restaurado.Should().NotBeNull();
        restaurado!.InformacionEmpleado.Should().BeNull();
        restaurado.Fecha.Should().Be(Fecha);
        restaurado.HorasDiscriminadas.MinutosPorConcepto.Should().BeEmpty();
        restaurado.HorasDiscriminadas.Trazabilidad.Should().BeEmpty();
    }
}
