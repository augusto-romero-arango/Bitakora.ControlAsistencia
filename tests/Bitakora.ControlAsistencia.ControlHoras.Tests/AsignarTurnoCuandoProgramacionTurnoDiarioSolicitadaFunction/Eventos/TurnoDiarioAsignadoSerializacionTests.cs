// HU-12: Asignar turno diario al control cuando se solicita programacion

using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

/// <summary>
/// Verifica que TurnoDiarioAsignado sobrevive un roundtrip de serializacion STJ.
/// Requerido porque Marten usa STJ con PropertyNamingPolicy=null (PascalCase)
/// y el evento tiene constructor privado y propiedades con private set.
/// Ver ADR-0013 y feedback de memoria: ConfigurarSerializacion es obligatorio.
///
/// Los literales JSON de estos tests fijan la forma canonica HACIA ADELANTE, no compatibilidad con
/// streams anteriores: cada vez que una clave persistida cambia, se reescriben y los streams viejos
/// se purgan en el mismo despliegue (MEF-ADR-0036 seccion 5). Sin esa purga, el payload reducido se
/// relee dejando campos en null SIN excepcion.
/// </summary>
public class TurnoDiarioAsignadoSerializacionTests
{
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000001");

    private static readonly ColaboradorProgramado ColaboradorDePrueba = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static readonly DateOnly Fecha = new DateOnly(2026, 3, 15);

    // Issue #288: Descripcion (dato derivado, texto del formato tecnico) es irrelevante para este
    // round-trip -> placeholder no vacio para verificar tambien que sobrevive la serializacion.
    private static readonly TurnoDiario TurnoDiarioDePrueba = new(
        "Turno Manana",
        [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "(08:00-16:00)")],
        "Turno Manana (08:00-16:00)");

    private static readonly string StreamId = $"cd:{ColaboradorDePrueba.CodigoColaborador}:{Fecha:yyyyMMdd}";

    // Regla 16: todo evento persistido en Marten debe tener test de serializacion roundtrip
    // Verifica con datos reales y completos (no listas vacias para VOs anidados)
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

        // Issue #288 CA-7: Descripcion (dato derivado persistido) sobrevive el round-trip,
        // tanto a nivel turno como a nivel de cada franja ordinaria.
        deserializado.DetalleTurno.Descripcion.Should().Be(TurnoDiarioDePrueba.Descripcion);
        deserializado.DetalleTurno.FranjasOrdinarias[0].Descripcion
            .Should().Be(TurnoDiarioDePrueba.FranjasOrdinarias[0].Descripcion);
    }

    // Nota de seguridad de datos (critica): JSON con la FORMA EXACTA que Marten escribe en
    // mt_events desde #401 (PascalCase, PropertyNamingPolicy=null) -- claves InformacionColaborador
    // (con CodigoColaborador anidado), Fecha, DetalleTurno y SolicitudId. En #322 este mismo test
    // acreditaba que un cambio de TIPO no alteraba la forma del wire; en #401 las claves si cambian,
    // asi que el literal se reescribio y lo que custodia ahora es la forma canonica hacia adelante.
    // La compatibilidad hacia atras se resuelve con la purga del mismo despliegue, no con mapeo
    // (MEF-ADR-0036 seccion 5). El alias del evento sigue intacto: la CLASE no se renombra.
    //
    // La comparacion es por igualdad estructural completa (.Should().Be() sobre el objeto
    // compuesto, no solo sus campos escalares): asi el test tambien exige que TurnoDiario/
    // FranjaProgramada repliquen la semantica de igualdad de los originales (CA-1), no solo su
    // forma de campos.
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

    // Issue #322 CA-2 (agregado en revision): el JSON de arriba lleva "Descansos": [] y
    // "Extras": [], asi que SubFranjaProgramada no participaba de ningun round-trip contra la forma
    // persistida. Los streams reales de dev si tienen turnos con sub-franjas: este test las incluye,
    // con offsets distintos entre inicio y fin, para que un cruce de campos al releer el JSON
    // ya escrito no pase inadvertido.
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

        // Igualdad de SubFranjaProgramada excluye Descripcion (CA-1), asi que el assert de arriba
        // no la cubre: se verifica aparte para que una perdida del dato derivado se vea.
        franja.Descansos[0].Descripcion.Should().Be("(23:50-00:10+1)");
        franja.Extras[0].Descripcion.Should().Be("(06:00+1-08:00+1)");
    }

    // Issue #322 CA-2 / Nota de seguridad de datos (agregado en revision): los eventos escritos
    // ANTES del issue #288 no llevan "Descripcion" en ningun nivel. STJ deja null en el parametro
    // posicional y los tres records lo normalizan a cadena vacia -- rama que ningun round-trip
    // ejercitaba sobre el evento persistido. Espejo de
    // ProgramacionTurnoSolicitadaSerializacionTests.Deserializar_NormalizaDescripcionACadenaVacia_*
    // (Programacion.Tests, issue #319).
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

        // El resto del payload se relee intacto: la ausencia del campo derivado no degrada la
        // identidad del turno (que lo excluye por diseno, CA-1).
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

    // Issue #336 CA-4: JSON persistido antes de este issue no lleva la clave "Sede" en ninguna
    // franja (ultimo parametro posicional, aditivo y opcional). A diferencia de Descripcion, aqui
    // null ES el valor correcto de retrocompatibilidad -- no hay normalizacion especial, "sin sede
    // asignada" es una franja real y valida (turno prearmado multi-sede sin resolver).
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

    // Issue #336 CA-5: round-trip Marten con sedes pobladas en las franjas de un turno partido --
    // incluye igualdad por valor de FranjaProgramada con sede (Equals custom ahora la compara).
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

    // Issue #462 CA-3: el CC dentro de la sede de la franja es un string opaco, sin validacion --
    // round trip Marten cuando la sede lo trae poblado.
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

    // Issue #462 CA-4: eventos persistidos antes de este issue traen la sede SIN la clave
    // "CentroDeCostos" -- STJ deja null en el parametro posicional opcional.
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
}
