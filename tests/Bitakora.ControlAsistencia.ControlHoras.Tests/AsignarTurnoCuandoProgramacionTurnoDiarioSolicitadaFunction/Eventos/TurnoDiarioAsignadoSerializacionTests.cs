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
/// Issue #322: InformacionEmpleado y DetalleTurno cambiaron de TIPO (ColaboradorProgramado/
/// TurnoDiario, propios de ControlHoras.DomainEvents) sin cambiar el nombre de propiedad.
///
/// Issue #401: la propiedad paso a InformacionColaborador y su campo anidado a CodigoColaborador --
/// aqui SI cambian las claves JSON de mt_events. El JSON literal de estos tests se reescribio a las
/// claves nuevas y por eso ya no acredita compatibilidad con los streams viejos: esos se purgan en
/// el mismo despliegue (MEF-ADR-0036 seccion 5). Lo que fija desde ahora es la forma canonica.
/// </summary>
public class TurnoDiarioAsignadoSerializacionTests
{
    private static readonly Guid SolicitudId =
        Guid.Parse("019600b0-0000-7000-8000-000000000001");

    private static readonly ColaboradorProgramado ColaboradorDePrueba = new(
        "EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");

    private static readonly DateOnly Fecha = new DateOnly(2026, 3, 15);

    // Issue #288: Descripcion (dato derivado, texto del formato tecnico) es irrelevante para este
    // round-trip -> placeholder no vacio para verificar tambien que sobrevive la serializacion.
    private static readonly TurnoDiario TurnoDiarioDePrueba = new(
        "Turno Manana",
        [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "(08:00-16:00)")],
        "Turno Manana (08:00-16:00)");

    private static readonly string StreamId = $"{ColaboradorDePrueba.CodigoColaborador}:{Fecha:yyyy-MM-dd}";

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
        deserializado.InformacionColaborador.CodigoColaborador.Should().Be(ColaboradorDePrueba.CodigoColaborador);
        deserializado.InformacionColaborador.Nombres.Should().Be(ColaboradorDePrueba.Nombres);
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
              "Id": "EMP-001:2026-03-15",
              "InformacionColaborador": {
                "CodigoColaborador": "EMP-001",
                "TipoIdentificacion": "CC",
                "NumeroIdentificacion": "1234567890",
                "Nombres": "Luis Augusto",
                "Apellidos": "Barreto"
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

        var colaboradorEsperado = new ColaboradorProgramado("EMP-001", "CC", "1234567890", "Luis Augusto", "Barreto");
        var turnoEsperado = new TurnoDiario(
            "Turno Manana",
            [new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, [], [], "(08:00-16:00)")],
            "Turno Manana (08:00-16:00)");

        deserializado.Should().NotBeNull();
        deserializado!.Id.Should().Be("EMP-001:2026-03-15");
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
              "Id": "EMP-001:2026-03-15",
              "InformacionColaborador": {
                "CodigoColaborador": "EMP-001",
                "TipoIdentificacion": "CC",
                "NumeroIdentificacion": "1234567890",
                "Nombres": "Luis Augusto",
                "Apellidos": "Barreto"
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
              "Id": "EMP-001:2026-03-15",
              "InformacionColaborador": {
                "CodigoColaborador": "EMP-001",
                "TipoIdentificacion": "CC",
                "NumeroIdentificacion": "1234567890",
                "Nombres": "Luis Augusto",
                "Apellidos": "Barreto"
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
              "Id": "EMP-001:2026-03-15",
              "InformacionColaborador": {
                "CodigoColaborador": "EMP-001",
                "TipoIdentificacion": "CC",
                "NumeroIdentificacion": "1234567890",
                "Nombres": "Luis Augusto",
                "Apellidos": "Barreto"
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
}
