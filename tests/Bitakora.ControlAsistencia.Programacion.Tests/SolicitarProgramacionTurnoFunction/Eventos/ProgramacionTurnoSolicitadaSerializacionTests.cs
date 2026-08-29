// Guardrail de la FORMA persistida de ProgramacionTurnoSolicitada, el evento del stream de
// SolicitudProgramacionAggregateRoot. Fija las claves JSON crudas de mt_events para que un cambio
// accidental de nombre de propiedad (o de campo anidado) no pase inadvertido: el JSON se construye
// como texto/tipo anonimo, sin pasar por el tipo bajo prueba, y se deserializa con las mismas
// opciones que Marten usa en produccion (CrearOpcionesMarten()).
//
// Issue #319 CA-2: cuando las propiedades Empleado/DetalleTurno cambiaron de TIPO
// (InformacionEmpleado/DetalleTurno de PublicEvents/PrivateEvents -> records propios del dominio),
// estos tests probaron que la forma del wire NO cambiaba -- STJ no persiste $type para
// records/clases anidadas, asi que los streams ya escritos seguian leyendose.
//
// Issue #401: aqui las claves SI cambian ("Empleado" -> "Colaborador", "EmpleadoId" ->
// "CodigoColaborador"); issue #436 las reduce de nuevo, del quinteto del colaborador a la terna de
// identidad ("Identificacion", "CodigoColaborador", "NombreCompleto"). Por eso estas formas se
// reescribieron a las claves NUEVAS y ya no
// demuestran compatibilidad hacia atras: los streams escritos con el vocabulario viejo no
// deserializan contra este tipo, y se purgan en el mismo despliegue que integra el cambio
// (MEF-ADR-0036 seccion 5, decision deliberada -- sin JsonPropertyName ni upcasters). Lo que
// estos tests custodian desde ahora es la forma canonica hacia adelante.
//
// La forma de prueba lleva descansos y extras poblados a proposito: es el UNICO sitio donde el
// nivel mas interno de la jerarquia persistida (SubFranjaProgramada, antes DetalleSubFranja) se
// deserializa desde la forma de mt_events. Con listas vacias ese nivel no se ejercitaria.

using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction.Eventos;

public class ProgramacionTurnoSolicitadaSerializacionTests
{
    private static readonly Guid Id = Guid.Parse("019600a0-0000-7000-8000-000000000050");
    private static readonly DateOnly Fecha1 = new(2026, 4, 7);
    private static readonly DateOnly Fecha2 = new(2026, 4, 8);

    // Descripcion en los tres niveles, con el formato tecnico que producen los ToString() de los
    // tipos ricos (issue #288). Son constantes porque los tests las afirman una a una: quedan
    // FUERA del Equals de los tres records (dato derivado, no identidad), asi que un
    // Should().Be(...) sobre el record completo es ciego a ellas.
    private const string DescripcionTurno =
        "Turno Manana (06:00-14:00)[Descansos:(12:00-12:30)][Extras:(13:00-14:00)]";
    private const string DescripcionFranja =
        "(06:00-14:00)[Descansos:(12:00-12:30)][Extras:(13:00-14:00)]";
    private const string DescripcionDescanso = "(12:00-12:30)";
    private const string DescripcionExtra = "(13:00-14:00)";

    private static readonly ColaboradorProgramado ColaboradorEsperado =
        new("CC-12345678", "E001", "Juan Perez");

    // Issue #331: sede efectiva del dia, campo aditivo y opcional.
    private static readonly SedeProgramada SedeEsperada = new("SEDE-01", "Sede Principal");

    private static readonly TurnoProgramado TurnoEsperado = new(
        "Turno Manana",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [new SubFranjaProgramada(new TimeOnly(12, 0), new TimeOnly(12, 30), 0, 0, DescripcionDescanso)],
                [new SubFranjaProgramada(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, DescripcionExtra)],
                DescripcionFranja)
        }.AsReadOnly(),
        DescripcionTurno);

    // Forma EXACTA con la que Marten persiste este evento desde #401: claves "Colaborador",
    // "Fechas", "DetalleTurno" y sus anidados ("CodigoColaborador" dentro de "Colaborador"). Un
    // tipo anonimo local basta para reproducir la FORMA sin referenciar los ensamblados de los
    // tipos reales: lo unico que importa para el guardrail es que las claves JSON coincidan --
    // construirla desde el propio tipo bajo prueba haria el test ciego a un rename.
    // Issue #462: forma persistida CON sede, pero sin la clave "CentroDeCostos" dentro de ella --
    // los streams escritos antes de este issue no la llevan. Objeto anonimo deliberadamente sin esa
    // clave (no se construye desde el tipo bajo prueba).
    private static string JsonConSedeSinCentroDeCostos()
    {
        var forma = new
        {
            Id,
            Colaborador = new
            {
                Identificacion = "CC-12345678",
                CodigoColaborador = "E001",
                NombreCompleto = "Juan Perez"
            },
            Fechas = new[] { Fecha1 },
            DetalleTurno = new
            {
                Nombre = "Turno Manana",
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = new TimeOnly(6, 0),
                        HoraFin = new TimeOnly(14, 0),
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>(),
                        Descripcion = "(06:00-14:00)"
                    }
                },
                Descripcion = "Turno Manana (06:00-14:00)"
            },
            Sede = new { Id = "SEDE-01", Nombre = "Sede Principal" }
        };

        return JsonSerializer.Serialize(forma, new JsonSerializerOptions { PropertyNamingPolicy = null });
    }

    private static string JsonConLaFormaPersistidaEnMtEvents()
    {
        var forma = new
        {
            Id,
            Colaborador = new
            {
                Identificacion = "CC-12345678",
                CodigoColaborador = "E001",
                NombreCompleto = "Juan Perez"
            },
            Fechas = new[] { Fecha1, Fecha2 },
            DetalleTurno = new
            {
                Nombre = "Turno Manana",
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = new TimeOnly(6, 0),
                        HoraFin = new TimeOnly(14, 0),
                        DiaOffsetFin = 0,
                        Descansos = new[]
                        {
                            new
                            {
                                HoraInicio = new TimeOnly(12, 0),
                                HoraFin = new TimeOnly(12, 30),
                                DiaOffsetInicio = 0,
                                DiaOffsetFin = 0,
                                Descripcion = DescripcionDescanso
                            }
                        },
                        Extras = new[]
                        {
                            new
                            {
                                HoraInicio = new TimeOnly(13, 0),
                                HoraFin = new TimeOnly(14, 0),
                                DiaOffsetInicio = 0,
                                DiaOffsetFin = 0,
                                Descripcion = DescripcionExtra
                            }
                        },
                        Descripcion = DescripcionFranja
                    }
                },
                Descripcion = DescripcionTurno
            }
        };

        // PropertyNamingPolicy = null es la politica que Marten usa (PascalCase tal cual), la misma
        // que fija CrearOpcionesMarten() al leer.
        return JsonSerializer.Serialize(forma, new JsonSerializerOptions { PropertyNamingPolicy = null });
    }

    // La forma anterior al issue #288 es exactamente la actual SIN las claves "Descripcion" -- ese
    // campo se agrego entonces, y los eventos escritos antes no lo llevan. Quitarlas del JSON
    // canonico expresa esa relacion mejor que reescribir la forma completa a mano.
    private static string JsonConFormaPersistidaSinDescripcion()
    {
        var nodo = JsonNode.Parse(JsonConLaFormaPersistidaEnMtEvents())!;
        QuitarDescripcion(nodo);
        return nodo.ToJsonString();
    }

    private static void QuitarDescripcion(JsonNode? nodo)
    {
        switch (nodo)
        {
            case JsonObject objeto:
                objeto.Remove("Descripcion");
                foreach (var (_, hijo) in objeto) QuitarDescripcion(hijo);
                break;
            case JsonArray arreglo:
                foreach (var hijo in arreglo) QuitarDescripcion(hijo);
                break;
        }
    }

    private static ProgramacionTurnoSolicitada Deserializar(string json)
    {
        var evento = JsonSerializer.Deserialize<ProgramacionTurnoSolicitada>(
            json, ConfiguracionSerializacionProgramacion.CrearOpcionesMarten());

        evento.Should().NotBeNull();
        return evento!;
    }

    [Fact]
    public void Deserializar_ReconstruyeEventoIdentico_ConLaFormaPersistidaEnMtEvents()
    {
        var evento = Deserializar(JsonConLaFormaPersistidaEnMtEvents());

        evento.Id.Should().Be(Id);
        evento.Colaborador.Should().Be(ColaboradorEsperado);
        evento.Fechas.Should().Equal(Fecha1, Fecha2);
        evento.DetalleTurno.Should().Be(TurnoEsperado);
    }

    // Descripcion queda FUERA del Equals de TurnoProgramado/FranjaProgramada/SubFranjaProgramada
    // (dato derivado, no identidad -- issues #129 y #288), asi que el Should().Be(...) del test
    // anterior NO la compara: sin este test, un cambio de tipos que perdiera la Descripcion ya
    // persistida pasaria en verde. Se afirma en los tres niveles de la jerarquia.
    [Fact]
    public void Deserializar_ConservaLaDescripcionDeLosTresNiveles_ConLaFormaPersistidaEnMtEvents()
    {
        var evento = Deserializar(JsonConLaFormaPersistidaEnMtEvents());
        var franja = evento.DetalleTurno.FranjasOrdinarias.Single();

        evento.DetalleTurno.Descripcion.Should().Be(DescripcionTurno);
        franja.Descripcion.Should().Be(DescripcionFranja);
        franja.Descansos.Single().Descripcion.Should().Be(DescripcionDescanso);
        franja.Extras.Single().Descripcion.Should().Be(DescripcionExtra);
    }

    // Los streams escritos antes del issue #288 no llevan la clave "Descripcion": los tres records
    // documentan que STJ deja null en el parametro posicional y que la propiedad la normaliza a
    // cadena vacia. Ese camino solo lo ejercita este test -- y es el que corre contra los eventos
    // mas viejos de mt_events.
    [Fact]
    public void Deserializar_NormalizaDescripcionACadenaVacia_CuandoElJsonPersistidoNoLlevaEseCampo()
    {
        var evento = Deserializar(JsonConFormaPersistidaSinDescripcion());
        var franja = evento.DetalleTurno.FranjasOrdinarias.Single();

        evento.DetalleTurno.Descripcion.Should().BeEmpty();
        franja.Descripcion.Should().BeEmpty();
        franja.Descansos.Single().Descripcion.Should().BeEmpty();
        franja.Extras.Single().Descripcion.Should().BeEmpty();
    }

    // La identidad del resto del payload no depende de Descripcion: el evento viejo (sin el campo)
    // reconstruye el mismo turno que el nuevo. Es la otra cara del Equals que la excluye.
    [Fact]
    public void Deserializar_ReconstruyeElMismoTurno_CuandoElJsonPersistidoNoLlevaDescripcion()
    {
        var evento = Deserializar(JsonConFormaPersistidaSinDescripcion());

        evento.Colaborador.Should().Be(ColaboradorEsperado);
        evento.DetalleTurno.Should().Be(TurnoEsperado);
    }

    // Issue #331 CA-4: retrocompatibilidad. Los streams escritos antes de este issue no llevan la
    // clave "Sede" en el JSON persistido -- STJ deja null en el parametro posicional opcional
    // (mismo mecanismo que Descripcion, #288/#319).
    [Fact]
    public void Deserializar_DejaSedeEnNull_CuandoElJsonPersistidoNoLlevaEseCampo()
    {
        var evento = Deserializar(JsonConLaFormaPersistidaEnMtEvents());

        evento.Sede.Should().BeNull();
    }

    // Issue #331 CA-5: round-trip de serializacion con Marten (CrearOpcionesMarten()) cuando la
    // sede esta poblada -- el evento persistido debe reconstruir SedeProgramada identica.
    [Fact]
    public void RoundTrip_PreservaLaSede_CuandoLaSedeEstaPoblada()
    {
        var evento = new ProgramacionTurnoSolicitada(
            Id, ColaboradorEsperado, [Fecha1], TurnoEsperado, SedeEsperada);
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Sede.Should().Be(SedeEsperada);
    }

    // Issue #341 CA-4: los streams escritos antes de este issue no llevan la clave "Sede" DENTRO de
    // cada franja. Es un eje distinto del test de arriba (la sede a nivel del evento, #331): la
    // cascada escribe por primera vez una sede en franjas que el catalogo dejo sin ella, asi que el
    // JSON viejo y el nuevo difieren tambien en ese nivel anidado.
    [Fact]
    public void Deserializar_DejaSedeDeCadaFranjaEnNull_CuandoElJsonPersistidoNoLlevaEseCampo()
    {
        var evento = Deserializar(JsonConLaFormaPersistidaEnMtEvents());

        evento.DetalleTurno.FranjasOrdinarias.Single().Sede.Should().BeNull();
    }

    // Issue #341 CA-5, cara persistida: mt_events es el contrato mas longevo del sistema
    // (CA-ADR-0029 decision #5), y desde este issue guarda la sede EFECTIVA de cada franja. La sede
    // de la franja es deliberadamente DISTINTA de la del evento: son dos ejes independientes (lo
    // efectivo por franja frente a lo solicitado a nivel del dia), y con la misma sede en ambos un
    // round-trip que confundiera un nivel con el otro pasaria en verde.
    [Fact]
    public void RoundTrip_PreservaLaSedeDeCadaFranja_CuandoLaCascadaLaDejoResuelta()
    {
        var sedeDeLaFranja = new SedeProgramada("SEDE-SUBA", "Suba");
        var turnoConSedePorFranja = new TurnoProgramado(
            "Turno Manana",
            new List<FranjaProgramada>
            {
                new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], DescripcionFranja, sedeDeLaFranja)
            }.AsReadOnly(),
            DescripcionTurno);
        var evento = new ProgramacionTurnoSolicitada(
            Id, ColaboradorEsperado, [Fecha1], turnoConSedePorFranja, SedeEsperada);
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DetalleTurno.FranjasOrdinarias.Single().Sede.Should().Be(sedeDeLaFranja);
        restaurado.Sede.Should().Be(SedeEsperada);
    }

    // Issue #462 CA-1: el CC dentro de SedeProgramada es un string opaco, sin validacion -- round
    // trip Marten a nivel del evento cuando la sede lo trae poblado.
    [Fact]
    public void RoundTrip_PreservaElCentroDeCostosDeLaSede_CuandoLaSedeLoTrae()
    {
        var sedeConCentroDeCostos = new SedeProgramada("SEDE-01", "Sede Principal", "CC-100");
        var evento = new ProgramacionTurnoSolicitada(
            Id, ColaboradorEsperado, [Fecha1], TurnoEsperado, sedeConCentroDeCostos);
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.Sede!.CentroDeCostos.Should().Be("CC-100");
    }

    // Issue #462 CA-4: streams persistidos antes de este issue traen la sede SIN la clave
    // "CentroDeCostos" -- STJ deja null en el parametro posicional opcional (mismo mecanismo que
    // Descripcion/Sede, #288/#331).
    [Fact]
    public void Deserializar_DejaCentroDeCostosEnNull_CuandoLaSedePersistidaNoLlevaEseCampo()
    {
        var evento = Deserializar(JsonConSedeSinCentroDeCostos());

        evento.Sede.Should().NotBeNull();
        evento.Sede!.CentroDeCostos.Should().BeNull();
    }
}
