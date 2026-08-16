// Issue #319 CA-2: guardrail decisivo de seguridad de datos. ProgramacionTurnoSolicitada SE
// PERSISTE (stream de SolicitudProgramacionAggregateRoot); sus propiedades Empleado/DetalleTurno
// cambiaron de tipo (InformacionEmpleado/DetalleTurno, PublicEvents/PrivateEvents) a los records
// propios del dominio (Empleado/TurnoProgramado, Programacion.DomainEvents) -- MISMOS nombres de
// propiedad y de campo anidado, tipos nuevos.
//
// Estos tests construyen el JSON con la FORMA en la que Marten ya persistio este evento antes del
// cambio de tipos (mismos nombres de propiedad, con los tipos viejos) y lo deserializan contra el
// tipo NUEVO usando las mismas opciones que Marten usa en produccion (CrearOpcionesMarten()). STJ
// no persiste $type para records/clases anidadas -- ningun dato existente se pierde. Sin este
// guardrail, el cambio de tipo podria romper en silencio la lectura de streams ya escritos.
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

    private static readonly ColaboradorProgramado EmpleadoEsperado =
        new("E001", "CC", "12345678", "Juan", "Perez");

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

    // Forma EXACTA en la que Marten persistio este evento ANTES de este issue: mismos nombres de
    // propiedad ("Empleado", "Fechas", "DetalleTurno" y sus anidados) que hoy tipan con
    // InformacionEmpleado/DetalleTurno (PublicEvents/PrivateEvents). Un tipo anonimo local basta
    // para reproducir la FORMA sin necesitar referenciar esos ensamblados: lo unico que importa
    // para el guardrail es que las claves JSON coincidan.
    private static string JsonConFormaPersistidaAntesDelCambioDeTipos()
    {
        var forma = new
        {
            Id,
            Empleado = new
            {
                EmpleadoId = "E001",
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "12345678",
                Nombres = "Juan",
                Apellidos = "Perez"
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
        var nodo = JsonNode.Parse(JsonConFormaPersistidaAntesDelCambioDeTipos())!;
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
    public void Deserializar_ReconstruyeEventoIdentico_ConLaFormaPersistidaAntesDelCambioDeTipos()
    {
        var evento = Deserializar(JsonConFormaPersistidaAntesDelCambioDeTipos());

        evento.Id.Should().Be(Id);
        evento.Empleado.Should().Be(EmpleadoEsperado);
        evento.Fechas.Should().Equal(Fecha1, Fecha2);
        evento.DetalleTurno.Should().Be(TurnoEsperado);
    }

    // Descripcion queda FUERA del Equals de TurnoProgramado/FranjaProgramada/SubFranjaProgramada
    // (dato derivado, no identidad -- issues #129 y #288), asi que el Should().Be(...) del test
    // anterior NO la compara: sin este test, un cambio de tipos que perdiera la Descripcion ya
    // persistida pasaria en verde. Se afirma en los tres niveles de la jerarquia.
    [Fact]
    public void Deserializar_ConservaLaDescripcionDeLosTresNiveles_ConLaFormaPersistidaAntesDelCambioDeTipos()
    {
        var evento = Deserializar(JsonConFormaPersistidaAntesDelCambioDeTipos());
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

        evento.Empleado.Should().Be(EmpleadoEsperado);
        evento.DetalleTurno.Should().Be(TurnoEsperado);
    }

    // Issue #331 CA-4: retrocompatibilidad. Los streams escritos antes de este issue no llevan la
    // clave "Sede" en el JSON persistido -- STJ deja null en el parametro posicional opcional
    // (mismo mecanismo que Descripcion, #288/#319).
    [Fact]
    public void Deserializar_DejaSedeEnNull_CuandoElJsonPersistidoNoLlevaEseCampo()
    {
        var evento = Deserializar(JsonConFormaPersistidaAntesDelCambioDeTipos());

        evento.Sede.Should().BeNull();
    }

    // Issue #331 CA-5: round-trip de serializacion con Marten (CrearOpcionesMarten()) cuando la
    // sede esta poblada -- el evento persistido debe reconstruir SedeProgramada identica.
    [Fact]
    public void RoundTrip_PreservaLaSede_CuandoLaSedeEstaPoblada()
    {
        var evento = new ProgramacionTurnoSolicitada(
            Id, EmpleadoEsperado, [Fecha1], TurnoEsperado, SedeEsperada);
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
        var evento = Deserializar(JsonConFormaPersistidaAntesDelCambioDeTipos());

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
            Id, EmpleadoEsperado, [Fecha1], turnoConSedePorFranja, SedeEsperada);
        var opciones = ConfiguracionSerializacionProgramacion.CrearOpcionesMarten();

        var json = JsonSerializer.Serialize(evento, opciones);
        var restaurado = JsonSerializer.Deserialize<ProgramacionTurnoSolicitada>(json, opciones);

        restaurado.Should().NotBeNull();
        restaurado!.DetalleTurno.FranjasOrdinarias.Single().Sede.Should().Be(sedeDeLaFranja);
        restaurado.Sede.Should().Be(SedeEsperada);
    }
}
