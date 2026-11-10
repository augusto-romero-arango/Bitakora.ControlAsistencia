using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.Eventos;

/// <summary>
/// Los literales JSON fijan la forma canonica HACIA ADELANTE, no compatibilidad con streams anteriores:
/// si una clave persistida cambia, se reescriben y los streams viejos se purgan en el mismo despliegue
/// (MEF-ADR-0036 seccion 5). Sin esa purga, el payload se relee con campos en null SIN excepcion.
/// </summary>
public class TurnoDiarioAsignadoSerializacionTests
{
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000001");

    private static readonly ColaboradorProgramado ColaboradorDePrueba = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static readonly DateOnly Fecha = new DateOnly(2026, 3, 15);

    // Descripcion no vacia para verificar que el dato derivado sobrevive la serializacion.
    private static readonly TurnoDiario TurnoDiarioDePrueba = new(
        "Turno Manana",
        [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "(08:00-16:00)")],
        "Turno Manana (08:00-16:00)");

    private static readonly string StreamId = $"cd:{ColaboradorDePrueba.CodigoColaborador}:{Fecha:yyyyMMdd}";

    [Fact]
    public void Deserializar_ReconstruyeEvento_ConTodosLosCampos()
    {
        var evento = new TurnoDiarioAsignado(StreamId, ColaboradorDePrueba, Fecha, TurnoDiarioDePrueba, SolicitudId);
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be(StreamId);
        deserializado.SolicitudId.Should().Be(SolicitudId);
        deserializado.Fecha.Should().Be(Fecha);
        deserializado.InformacionColaborador.Should().Be(ColaboradorDePrueba);
        deserializado.DetalleTurno.Nombre.Should().Be(TurnoDiarioDePrueba.Nombre);
        deserializado.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        deserializado.DetalleTurno.FranjasOrdinarias[0].HoraInicio
            .Should().Be(new TimeOnly(8, 0));

        deserializado.DetalleTurno.Descripcion.Should().Be(TurnoDiarioDePrueba.Descripcion);
        deserializado.DetalleTurno.FranjasOrdinarias[0].Descripcion
            .Should().Be(TurnoDiarioDePrueba.FranjasOrdinarias[0].Descripcion);
    }

    // JSON con la forma exacta que Marten escribe en mt_events (PascalCase). La comparacion es por
    // igualdad estructural completa: tambien exige que TurnoDiario/FranjaProgramada repliquen la
    // igualdad de los originales, no solo su forma de campos.
    [Fact]
    public void Deserializar_ReconstruyeIdentico_DesdeElJsonConLaFormaYaPersistida()
    {
        const string jsonPersistidoHoy = """
            {
              "Id": "cd:EMP-001:20260315",
              "InformacionColaborador": {
                "Identificacion": "CC-1234567890",
                "CodigoColaborador": "EMP-001",
                "NombreCompleto": "Luis Augusto Barreto"
              },
              "Fecha": "2026-03-15",
              "DetalleTurno": {
                "Nombre": "Turno Manana",
                "FranjasOrdinarias": [
                  {
                    "HoraInicio": "08:00:00",
                    "HoraFin": "16:00:00",
                    "DiaOffsetFin": 0,
                    "Descansos": [],
                    "Extras": [],
                    "Descripcion": "(08:00-16:00)"
                  }
                ],
                "Descripcion": "Turno Manana (08:00-16:00)"
              },
              "SolicitudId": "019600b0-0000-7000-8000-000000000001"
            }
            """;
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(jsonPersistidoHoy, opciones);

        var colaboradorEsperado = new ColaboradorProgramado("CC-1234567890", "EMP-001", "Luis Augusto Barreto");
        var turnoEsperado = new TurnoDiario(
            "Turno Manana",
            [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "(08:00-16:00)")],
            "Turno Manana (08:00-16:00)");

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be("cd:EMP-001:20260315");
        deserializado.SolicitudId.Should().Be(Guid.Parse("019600b0-0000-7000-8000-000000000001"));
        deserializado.Fecha.Should().Be(new DateOnly(2026, 3, 15));
        deserializado.InformacionColaborador.Should().Be(colaboradorEsperado);
        deserializado.DetalleTurno.Should().Be(turnoEsperado);
    }

    // Incluye sub-franjas con offsets distintos entre inicio y fin para que un cruce de campos al releer
    // el JSON persistido no pase inadvertido.
    [Fact]
    public void Deserializar_ReconstruyeLasSubFranjasAnidadas_ConLaFormaPersistidaQueLlevaDescansosYExtras()
    {
        const string jsonPersistidoConSubFranjas = """
            {
              "Id": "cd:EMP-001:20260315",
              "InformacionColaborador": {
                "Identificacion": "CC-1234567890",
                "CodigoColaborador": "EMP-001",
                "NombreCompleto": "Luis Augusto Barreto"
              },
              "Fecha": "2026-03-15",
              "DetalleTurno": {
                "Nombre": "Turno Nocturno",
                "FranjasOrdinarias": [
                  {
                    "HoraInicio": "22:00:00",
                    "HoraFin": "06:00:00",
                    "DiaOffsetFin": 1,
                    "Descansos": [
                      {
                        "HoraInicio": "23:50:00",
                        "HoraFin": "00:10:00",
                        "DiaOffsetInicio": 0,
                        "DiaOffsetFin": 1,
                        "Descripcion": "(23:50-00:10+1)"
                      }
                    ],
                    "Extras": [
                      {
                        "HoraInicio": "06:00:00",
                        "HoraFin": "08:00:00",
                        "DiaOffsetInicio": 1,
                        "DiaOffsetFin": 1,
                        "Descripcion": "(06:00+1-08:00+1)"
                      }
                    ],
                    "Descripcion": "(22:00-06:00+1)"
                  }
                ],
                "Descripcion": "Turno Nocturno (22:00-06:00+1)"
              },
              "SolicitudId": "019600b0-0000-7000-8000-000000000001"
            }
            """;
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(
            jsonPersistidoConSubFranjas, opciones);

        var descansoEsperado = new SubFranjaProgramada(
            new TimeOnly(23, 50), new TimeOnly(0, 10), 0, 1, "(23:50-00:10+1)");
        var extraEsperada = new SubFranjaProgramada(
            new TimeOnly(6, 0), new TimeOnly(8, 0), 1, 1, "(06:00+1-08:00+1)");

        deserializado.Should().NotBeNull();
        var franja = deserializado!.DetalleTurno.FranjasOrdinarias.Should().ContainSingle().Subject;
        franja.Descansos.Should().ContainSingle().Which.Should().Be(descansoEsperado);
        franja.Extras.Should().ContainSingle().Which.Should().Be(extraEsperada);

        // La igualdad de SubFranjaProgramada excluye Descripcion: se verifica aparte.
        franja.Descansos[0].Descripcion.Should().Be("(23:50-00:10+1)");
        franja.Extras[0].Descripcion.Should().Be("(06:00+1-08:00+1)");
    }

    // JSON anterior a #288, sin "Descripcion": STJ deja null y los tres records lo normalizan a cadena
    // vacia. Espejo en ProgramacionTurnoSolicitadaSerializacionTests (Programacion.Tests).
    [Fact]
    public void Deserializar_NormalizaDescripcionACadenaVaciaEnLosTresNiveles_CuandoElJsonPersistidoNoLlevaEseCampo()
    {
        const string jsonPersistidoAntesDeDescripcion = """
            {
              "Id": "cd:EMP-001:20260315",
              "InformacionColaborador": {
                "Identificacion": "CC-1234567890",
                "CodigoColaborador": "EMP-001",
                "NombreCompleto": "Luis Augusto Barreto"
              },
              "Fecha": "2026-03-15",
              "DetalleTurno": {
                "Nombre": "Turno Manana",
                "FranjasOrdinarias": [
                  {
                    "HoraInicio": "08:00:00",
                    "HoraFin": "16:00:00",
                    "DiaOffsetFin": 0,
                    "Descansos": [
                      {
                        "HoraInicio": "12:00:00",
                        "HoraFin": "13:00:00",
                        "DiaOffsetInicio": 0,
                        "DiaOffsetFin": 0
                      }
                    ],
                    "Extras": []
                  }
                ]
              },
              "SolicitudId": "019600b0-0000-7000-8000-000000000001"
            }
            """;
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(
            jsonPersistidoAntesDeDescripcion, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.DetalleTurno.Descripcion.Should().BeEmpty();

        var franja = deserializado.DetalleTurno.FranjasOrdinarias.Should().ContainSingle().Subject;
        franja.Descripcion.Should().BeEmpty();
        franja.Descansos.Should().ContainSingle().Which.Descripcion.Should().BeEmpty();

        deserializado.DetalleTurno.Should().Be(new TurnoDiario(
            "Turno Manana",
            [
                new FranjaProgramada(
                    new TimeOnly(8, 0), new TimeOnly(16, 0), 0,
                    [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(13, 0), 0, 0, "")],
                    [], "")
            ],
            ""));
    }

    // JSON sin "Sede" (anterior a #336): aqui null es el valor correcto, sin normalizar; "sin sede
    // asignada" es una franja valida.
    [Fact]
    public void Deserializar_DejaLaSedeEnNull_CuandoElJsonPersistidoNoLlevaLaClaveSede()
    {
        const string jsonPersistidoSinSede = """
            {
              "Id": "cd:EMP-001:20260315",
              "InformacionColaborador": {
                "Identificacion": "CC-1234567890",
                "CodigoColaborador": "EMP-001",
                "NombreCompleto": "Luis Augusto Barreto"
              },
              "Fecha": "2026-03-15",
              "DetalleTurno": {
                "Nombre": "Turno Manana",
                "FranjasOrdinarias": [
                  {
                    "HoraInicio": "08:00:00",
                    "HoraFin": "16:00:00",
                    "DiaOffsetFin": 0,
                    "Descansos": [],
                    "Extras": [],
                    "Descripcion": "(08:00-16:00)"
                  }
                ],
                "Descripcion": "Turno Manana (08:00-16:00)"
              },
              "SolicitudId": "019600b0-0000-7000-8000-000000000001"
            }
            """;
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(jsonPersistidoSinSede, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.DetalleTurno.FranjasOrdinarias.Should().ContainSingle()
            .Which.Sede.Should().BeNull();
    }

    // La igualdad de FranjaProgramada incluye la sede.
    [Fact]
    public void Deserializar_ReconstruyeIdentico_ConSedesPobladasEnLasFranjas()
    {
        var sedeSuba = new SedeProgramada("SEDE-SUBA", "Suba");
        var sedeChapinero = new SedeProgramada("SEDE-CHAPINERO", "Chapinero");
        var turnoConSedes = new TurnoDiario(
            "Turno Partido",
            [
                new FranjaProgramada(
                    new TimeOnly(6, 0), new TimeOnly(10, 0), 0, [], [], "(06:00-10:00)", sedeSuba),
                new FranjaProgramada(
                    new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "(14:00-18:00)", sedeChapinero)
            ],
            "Turno Partido");
        var evento = new TurnoDiarioAsignado(StreamId, ColaboradorDePrueba, Fecha, turnoConSedes, SolicitudId);
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.DetalleTurno.Should().Be(turnoConSedes);
        deserializado.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeSuba);
        deserializado.DetalleTurno.FranjasOrdinarias[1].Sede.Should().Be(sedeChapinero);
    }

    [Fact]
    public void Deserializar_ReconstruyeIdentico_ConCentroDeCostosPobladoEnLaSede()
    {
        var sedeConCentroDeCostos = new SedeProgramada("SEDE-SUBA", "Suba", "CC-100");
        var turnoConSede = new TurnoDiario(
            "Turno Manana",
            [new FranjaProgramada(
                new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "(08:00-16:00)", sedeConCentroDeCostos)],
            "Turno Manana (08:00-16:00)");
        var evento = new TurnoDiarioAsignado(StreamId, ColaboradorDePrueba, Fecha, turnoConSede, SolicitudId);
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(json, opciones);

        deserializado.Should().NotBeNull();
        deserializado!.DetalleTurno.FranjasOrdinarias[0].Sede!.CentroDeCostos.Should().Be("CC-100");
    }

    // El campo es el ULTIMO parametro posicional y opcional del record: por eso STJ lo deja en
    // null ante un JSON sin la clave. Reordenar los parametros rompe esa compatibilidad.
    [Fact]
    public void Deserializar_DejaCentroDeCostosEnNull_CuandoLaSedePersistidaNoLlevaEseCampo()
    {
        const string jsonPersistidoSinCentroDeCostos = """
            {
              "Id": "cd:EMP-001:20260315",
              "InformacionColaborador": {
                "Identificacion": "CC-1234567890",
                "CodigoColaborador": "EMP-001",
                "NombreCompleto": "Luis Augusto Barreto"
              },
              "Fecha": "2026-03-15",
              "DetalleTurno": {
                "Nombre": "Turno Manana",
                "FranjasOrdinarias": [
                  {
                    "HoraInicio": "08:00:00",
                    "HoraFin": "16:00:00",
                    "DiaOffsetFin": 0,
                    "Descansos": [],
                    "Extras": [],
                    "Descripcion": "(08:00-16:00)",
                    "Sede": { "Id": "SEDE-SUBA", "Nombre": "Suba" }
                  }
                ],
                "Descripcion": "Turno Manana (08:00-16:00)"
              },
              "SolicitudId": "019600b0-0000-7000-8000-000000000001"
            }
            """;
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();

        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(
            jsonPersistidoSinCentroDeCostos, opciones);

        deserializado.Should().NotBeNull();
        var sede = deserializado!.DetalleTurno.FranjasOrdinarias[0].Sede;
        sede.Should().NotBeNull();
        sede!.CentroDeCostos.Should().BeNull();
    }

    [Fact]
    public void Deserializar_LeeAmplitudCero_CuandoJsonNoTraeLaClave()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var evento = new TurnoDiarioAsignado(StreamId, ColaboradorDePrueba, Fecha, TurnoDiarioDePrueba, SolicitudId);
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(evento, opciones))!;
        json["DetalleTurno"]!.AsObject().Remove("AmplitudVentanaEntradaEnMinutos");

        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(json.ToJsonString(), opciones);

        deserializado!.DetalleTurno.AmplitudVentanaEntradaEnMinutos.Should().Be(0);
    }

    [Fact]
    public void Deserializar_LeeMarcaEnFalse_CuandoJsonNoTraeLaClave()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var evento = new TurnoDiarioAsignado(StreamId, ColaboradorDePrueba, Fecha, TurnoDiarioDePrueba, SolicitudId);
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(evento, opciones))!;
        json["DetalleTurno"]!.AsObject().Remove("EsHorarioAbierto");

        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(json.ToJsonString(), opciones);

        deserializado!.DetalleTurno.EsHorarioAbierto.Should().BeFalse();
    }

    [Fact]
    public void Deserializar_ConservaMarca_CuandoTurnoEsDeHorarioAbierto()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var turno = TurnoDiarioDePrueba with { EsHorarioAbierto = true };
        var evento = new TurnoDiarioAsignado(StreamId, ColaboradorDePrueba, Fecha, turno, SolicitudId);

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(json, opciones);

        deserializado!.DetalleTurno.EsHorarioAbierto.Should().BeTrue();
    }

    [Fact]
    public void Deserializar_ConservaAmplitud_CuandoTurnoTieneVentana()
    {
        var opciones = ConfiguracionSerializacionControlHoras.CrearOpcionesMarten();
        var turno = TurnoDiarioDePrueba with { AmplitudVentanaEntradaEnMinutos = 180 };
        var evento = new TurnoDiarioAsignado(StreamId, ColaboradorDePrueba, Fecha, turno, SolicitudId);

        var json = JsonSerializer.Serialize(evento, opciones);
        var deserializado = JsonSerializer.Deserialize<TurnoDiarioAsignado>(json, opciones);

        deserializado!.DetalleTurno.AmplitudVentanaEntradaEnMinutos.Should().Be(180);
    }
}
