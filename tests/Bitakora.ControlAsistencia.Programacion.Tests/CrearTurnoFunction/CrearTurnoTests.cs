// Issue #335: tests del mapeo CrearTurno.ToDatosFranjas() -- traduce el contrato HTTP a la
// entrada del factory de TurnoCreado. Cubre la propagacion (o no) de la sede prearmada por franja.

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;

public class CrearTurnoTests
{
    private const string NombreTurno = "Turno Manana";

    // Mismas opciones que RequestValidator.ValidarAsync (req.ReadFromJsonAsync<T>): camelCase,
    // case-insensitive -- sin levantar el host.
    private static readonly JsonSerializerOptions OpcionesWeb = new(JsonSerializerDefaults.Web);

    // CA-1: la sede prearmada de la franja del comando llega a DatosFranja.
    [Fact]
    public void ToDatosFranjas_PropagaLaSedeDeCadaFranja_CuandoComandoTraeSedes()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");
        var comando = new CrearTurno(Guid.NewGuid(), NombreTurno,
            [new CrearTurno.Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], sede)]);

        var datos = comando.ToDatosFranjas();

        datos[0].Sede.Should().Be(sede);
    }

    // CA-2: regresion -- una franja sin sede en el comando conserva el comportamiento actual.
    [Fact]
    public void ToDatosFranjas_DejaSedeNull_CuandoFranjaDelComandoNoTraeSede()
    {
        var comando = new CrearTurno(Guid.NewGuid(), NombreTurno,
            [new CrearTurno.Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

        var datos = comando.ToDatosFranjas();

        datos[0].Sede.Should().BeNull();
    }

    // CA-5: un body sin la clave "ordinarias" llega con Ordinarias == null -- ToDatosFranjas() lo
    // trata como lista vacia, no como error.
    [Fact]
    public void ToDatosFranjas_DevuelveListaVacia_CuandoOrdinariasEsNull()
    {
        var comando = new CrearTurno(Guid.NewGuid(), NombreTurno, null);

        var datos = comando.ToDatosFranjas();

        datos.Should().BeEmpty();
    }

    [Fact]
    public void Deserializar_MapeaDescansosYExtras_CuandoOrdinariaTraeHijasConFormatoHHmm()
    {
        const string json = """
            {
              "turnoId": "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b",
              "nombre": "Diurno",
              "ordinarias": [
                {
                  "inicio": "06:00",
                  "fin": "14:00",
                  "descansos": [{"inicio":"10:00","fin":"10:15"}],
                  "extras": [{"inicio":"14:00","fin":"15:00"}]
                }
              ]
            }
            """;

        var comando = JsonSerializer.Deserialize<CrearTurno>(json, OpcionesWeb);

        comando!.Ordinarias![0].Descansos![0].Should()
            .Be(new CrearTurno.Rango(new TimeOnly(10, 0), new TimeOnly(10, 15)));
        comando.Ordinarias[0].Extras![0].Should()
            .Be(new CrearTurno.Rango(new TimeOnly(14, 0), new TimeOnly(15, 0)));
    }

    [Fact]
    public void Deserializar_AceptaHoraConSegundos_CuandoFormatoEsHHmmss()
    {
        const string json = """
            {
              "turnoId": "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b",
              "nombre": "Diurno",
              "ordinarias": [{"inicio":"06:00:00","fin":"14:00:00"}]
            }
            """;

        var comando = JsonSerializer.Deserialize<CrearTurno>(json, OpcionesWeb);

        comando!.Ordinarias![0].Inicio.Should().Be(new TimeOnly(6, 0));
        comando.Ordinarias[0].Fin.Should().Be(new TimeOnly(14, 0));
    }

    [Fact]
    public void Deserializar_LanzaJsonException_CuandoHoraEs2400()
    {
        const string json = """
            {
              "turnoId": "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b",
              "nombre": "Diurno",
              "ordinarias": [{"inicio":"24:00","fin":"06:00"}]
            }
            """;

        var act = () => JsonSerializer.Deserialize<CrearTurno>(json, OpcionesWeb);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void ToDatosFranjas_DevuelveHijasVaciasYOffsetCero_CuandoOrdinariaJsonNoTraeHijasNiOffset()
    {
        const string json = """
            {
              "turnoId": "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b",
              "nombre": "Diurno",
              "ordinarias": [{"inicio":"06:00","fin":"14:00"}]
            }
            """;
        var comando = JsonSerializer.Deserialize<CrearTurno>(json, OpcionesWeb)!;

        var datos = comando.ToDatosFranjas();

        datos[0].Descansos.Should().BeEmpty();
        datos[0].Extras.Should().BeEmpty();
        datos[0].DiaOffsetFin.Should().Be(0);
    }

    [Fact]
    public void ToDatosFranjas_MapeaRangosATuplasYPropagaOffsetYSede_CuandoFranjaTraeHijasOffsetYSede()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");
        var comando = new CrearTurno(Guid.NewGuid(), NombreTurno,
            [new CrearTurno.Franja(
                new TimeOnly(6, 0), new TimeOnly(14, 0),
                Descansos: [new CrearTurno.Rango(new TimeOnly(10, 0), new TimeOnly(10, 15))],
                Extras: [new CrearTurno.Rango(new TimeOnly(14, 0), new TimeOnly(15, 0))],
                Sede: sede,
                DiaOffsetFin: 1)]);

        var datos = comando.ToDatosFranjas();

        datos[0].Descansos.Should().ContainSingle()
            .Which.Should().Be((new TimeOnly(10, 0), new TimeOnly(10, 15)));
        datos[0].Extras.Should().ContainSingle()
            .Which.Should().Be((new TimeOnly(14, 0), new TimeOnly(15, 0)));
        datos[0].DiaOffsetFin.Should().Be(1);
        datos[0].Sede.Should().Be(sede);
    }

    [Fact]
    public void ToDatosFranjas_DejaOffsetEnCero_CuandoDiaOffsetFinEsNull()
    {
        var comando = new CrearTurno(Guid.NewGuid(), NombreTurno,
            [new CrearTurno.Franja(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

        var datos = comando.ToDatosFranjas();

        datos[0].DiaOffsetFin.Should().Be(0);
    }
}
