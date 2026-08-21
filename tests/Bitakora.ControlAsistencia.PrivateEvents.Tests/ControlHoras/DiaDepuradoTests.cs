// Issue #421: guardrail de portabilidad por el bus para DiaDepurado (reclasificado de DiaCalculado,
// IPublicEvent -> IPrivateEvent).

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.PrivateEvents.Tests.ControlHoras;

/// <summary>
/// Verifica que DiaDepurado (IPrivateEvent) sobrevive el cruce fisico del ASB interno del BC
/// (MEF-ADR-0024 decision #3). Nunca se persiste (IdentidadEventosControlHoras lo excluye
/// explicitamente), asi que no hay round-trip de Marten que cubrir (seccion 6d del test-writer):
/// solo el canal del bus (seccion 6e), donde la expectativa es que el payload SOBREVIVA sin
/// resolver custom -- si no sobreviviera, el tipo no seria portable (MEF-ADR-0023).
///
/// Este test compila referenciando UNICAMENTE PrivateEvents (CA-ADR-0029 seccion 3: si necesitara
/// mas, el tipo no seria portable).
/// </summary>
public class DiaDepuradoTests
{
    private static JsonSerializerOptions CrearOpcionesDelBus() =>
        new(JsonSerializerDefaults.Web);

    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static HorasDiscriminadas HorasConDatos() => new(
        new Dictionary<string, int>
        {
            ["DominicalFestivaDiurna"] = 420,
            ["ExtraDiurnaDominicalFestiva"] = 30,
            ["Retardo"] = 15
        },
        ["06:00-14:00: Dominical festiva diurna"]);

    // CA-1/CA-4: round-trip con el serializador POR DEFECTO del bus preserva CodigoColaborador
    // (top-level, siempre presente), Fecha, Colaborador y HorasDiscriminadas.
    [Fact]
    public void RoundTrip_PreservaTodosLosCampos_ConSerializadorPorDefectoDelBus()
    {
        var colaborador = new ResumenColaborador("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
        var evento = new DiaDepurado("EMP-001", Fecha, colaborador, HorasConDatos());
        var opciones = CrearOpcionesDelBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado.Should().Be(evento);
    }

    // CA-4: cuando el dia nace solo por marcacion (sin turno previo), Colaborador viaja null pero
    // CodigoColaborador top-level sigue presente -- el defecto latente que este issue corrige.
    [Fact]
    public void RoundTrip_PreservaCodigoColaboradorTopLevel_CuandoColaboradorEsNulo()
    {
        var evento = new DiaDepurado(
            "EMP-002", Fecha, null, new HorasDiscriminadas(new Dictionary<string, int>(), []));
        var opciones = CrearOpcionesDelBus();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<DiaDepurado>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.CodigoColaborador.Should().Be("EMP-002");
        restaurado.Colaborador.Should().BeNull();
    }
}
