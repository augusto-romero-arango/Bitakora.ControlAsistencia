using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaSemanalFunction;

public class CrearPlantillaSemanalCommandHandlerTests : CommandHandlerAsyncTest<CrearPlantillaSemanal>
{
    private const string NombrePlantilla = "Semana Cocina";

    // Catalogo vigente que el handler ve (vista CuadroSemanalTurnos). Vacio por defecto: el
    // camino feliz de #620 no necesita conocer el puerto nuevo. Reasignarlo SIEMPRE antes de
    // WhenAsync: Handler se construye recien al ejecutar el comando.
    private FakeLectorNombresPlantillaSemanal _lector = new();

    protected override ICommandHandlerAsync<CrearPlantillaSemanal> Handler =>
        new CrearPlantillaSemanalCommandHandler(EventStore, _lector);

    // Siembra una plantilla ya creada en el catalogo: el evento en su propio stream (lo que el
    // event store ve) y su nombre en el lector (lo que la vista le devuelve al handler).
    private Guid SembrarPlantillaEnCatalogo(string nombre)
    {
        var plantillaId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c");
        Given(plantillaId.ToString(), PlantillaSemanalCreada.Crear(plantillaId, nombre, 2));
        _lector = new FakeLectorNombresPlantillaSemanal(nombre);
        return plantillaId;
    }

    [Fact]
    public async Task CrearPlantillaSemanal_EmitePlantillaSemanalCreadaYEstableceId_CuandoPlantillaNoExiste()
    {
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2);
        var eventoEsperado = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaSemanal_EmitePlantillaSemanalCreada_CuandoSemanasEsElMaximoPermitido()
    {
        var comando = new CrearPlantillaSemanal(
            GuidAggregateId, NombrePlantilla, PlantillaSemanalCreada.MaximoSemanas);
        var eventoEsperado = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaSemanal_LanzaRecursoYaExisteException_CuandoPlantillaYaExiste()
    {
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2);
        var eventoPrevio = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);

        Given(eventoPrevio);

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearPlantillaSemanalCommandHandler.Mensajes.PlantillaYaExiste}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    // CA-4: PlantillaId ya existente Y nombre tambien duplicado -> gana PlantillaYaExiste (el
    // chequeo de existencia va antes que el de nombre en el orden de declines del handler).
    [Fact]
    public async Task CrearPlantillaSemanal_LanzaPlantillaYaExiste_CuandoPlantillaIdYaExisteYNombreTambienEstaDuplicado()
    {
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2);
        var eventoPrevio = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);
        Given(eventoPrevio);
        _lector = new FakeLectorNombresPlantillaSemanal(NombrePlantilla);

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearPlantillaSemanalCommandHandler.Mensajes.PlantillaYaExiste}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    // CA-3: catalogo vacio (ninguna plantilla vigente) -> el camino feliz de #620 sigue igual.
    [Fact]
    public async Task CrearPlantillaSemanal_EmitePlantillaSemanalCreada_CuandoElCatalogoEstaVacio()
    {
        _lector = new FakeLectorNombresPlantillaSemanal();
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2);
        var eventoEsperado = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    // CA-3: catalogo con otros nombres (ninguno coincide, exacto ni normalizado) -> se crea
    // normalmente.
    [Fact]
    public async Task CrearPlantillaSemanal_EmitePlantillaSemanalCreada_CuandoNingunNombreDelCatalogoCoincide()
    {
        _lector = new FakeLectorNombresPlantillaSemanal("Semana Aseo", "Semana Vigilancia");
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2);
        var eventoEsperado = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    // CA-1: nombre coincide EXACTAMENTE con uno vigente del catalogo -> 409, sin escribir nada.
    [Fact]
    public async Task CrearPlantillaSemanal_LanzaReglaDeNegocioDeclinadaException_CuandoNombreCoincideExactamenteConUnoDelCatalogo()
    {
        var plantillaExistenteId = SembrarPlantillaEnCatalogo(NombrePlantilla);
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2);

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearPlantillaSemanalCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(
            plantillaExistenteId.ToString(), p => p.Id, plantillaExistenteId.ToString());
    }

    // CA-2: nombre difiere solo en mayusculas/espacios (trim + colapso + case-insensitive) de uno
    // vigente -> 409.
    [Fact]
    public async Task CrearPlantillaSemanal_LanzaReglaDeNegocioDeclinadaException_CuandoNombreDifiereSoloEnMayusculasYEspaciosDeUnoDelCatalogo()
    {
        const string nombreConEspaciosYMayusculas = "  semana   COCINA ";
        var plantillaExistenteId = SembrarPlantillaEnCatalogo(NombrePlantilla);
        var comando = new CrearPlantillaSemanal(GuidAggregateId, nombreConEspaciosYMayusculas, 2);

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearPlantillaSemanalCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(
            plantillaExistenteId.ToString(), p => p.Id, plantillaExistenteId.ToString());
    }

    // CA-2, lado espejo: la normalizacion se aplica tambien al nombre QUE VIENE DEL CATALOGO, no
    // solo al del comando -- un cuadro guardado con espacios/mayusculas de sobra igual colisiona.
    [Fact]
    public async Task CrearPlantillaSemanal_LanzaReglaDeNegocioDeclinadaException_CuandoElNombreDelCatalogoDifiereSoloEnMayusculasYEspacios()
    {
        var plantillaExistenteId = SembrarPlantillaEnCatalogo("  SEMANA   cocina ");
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2);

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearPlantillaSemanalCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(
            plantillaExistenteId.ToString(), p => p.Id, plantillaExistenteId.ToString());
    }

    // CA-2: nombre difiere solo en acentos de uno vigente -> se crea normalmente (decision del
    // experto en #497: normalizar acentos abre falsos positivos, los acentos SON significativos).
    [Fact]
    public async Task CrearPlantillaSemanal_EmitePlantillaSemanalCreada_CuandoNombreDifiereSoloEnAcentosDeUnoDelCatalogo()
    {
        const string nombreConAcento = "Semana Cociña";
        _lector = new FakeLectorNombresPlantillaSemanal(NombrePlantilla);
        var comando = new CrearPlantillaSemanal(GuidAggregateId, nombreConAcento, 2);
        var eventoEsperado = PlantillaSemanalCreada.Crear(comando.PlantillaId, comando.Nombre, comando.Semanas);

        Given();
        await WhenAsync(comando);

        Then(eventoEsperado);
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    // CA-4: nombre duplicado Y Semanas invalido (0, fuera de rango) -> gana NombreDuplicado: el
    // chequeo de nombre corre antes que el factory de PlantillaSemanalCreada.
    [Fact]
    public async Task CrearPlantillaSemanal_LanzaNombreDuplicado_CuandoNombreEstaDuplicadoYSemanasEsInvalido()
    {
        var plantillaExistenteId = SembrarPlantillaEnCatalogo(NombrePlantilla);
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 0);

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearPlantillaSemanalCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(
            plantillaExistenteId.ToString(), p => p.Id, plantillaExistenteId.ToString());
    }

    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000b01");

    // El TestStore no puebla AggregateRoot.Version: la version copiada es 0 en este harness.
    private const long VersionEnElHarness = 0;

    private static LimitesJornada Limites(int semanales) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1);

    // CA-2
    [Fact]
    public async Task CrearPlantillaSemanal_EmiteCreadaYJornadaAsignada_CuandoLaJornadaExiste()
    {
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2, JornadaId);
        Given(JornadaId.ToString(), JornadaCreada.Crear(JornadaId, Limites(42)));

        await WhenAsync(comando);

        Then(PlantillaSemanalCreada.Crear(GuidAggregateId, NombrePlantilla, 2),
            JornadaDePlantillaSemanalAsignada.Crear(GuidAggregateId, JornadaId, Limites(42), VersionEnElHarness));
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, JornadaId);
    }

    // CA-3
    [Fact]
    public async Task CrearPlantillaSemanal_LanzaRecursoNoEncontradoException_CuandoLaJornadaNoTieneStream()
    {
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2, JornadaId);
        Given();

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{CrearPlantillaSemanalCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    // El nombre duplicado se evalua antes que la Jornada: un 409 no se enmascara con un 404.
    [Fact]
    public async Task CrearPlantillaSemanal_LanzaNombreDuplicado_CuandoNombreEstaDuplicadoYLaJornadaNoExiste()
    {
        var plantillaExistenteId = SembrarPlantillaEnCatalogo(NombrePlantilla);
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2, JornadaId);

        var act = async () => await WhenAsync(comando);

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearPlantillaSemanalCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaSemanalTurnos, string>(
            plantillaExistenteId.ToString(), p => p.Id, plantillaExistenteId.ToString());
    }

    // CA-1: sin JornadaId la plantilla nace sin Jornada.
    [Fact]
    public async Task CrearPlantillaSemanal_EmiteSoloCreada_CuandoNoSeIndicaJornada()
    {
        var comando = new CrearPlantillaSemanal(GuidAggregateId, NombrePlantilla, 2, null);
        Given(JornadaId.ToString(), JornadaCreada.Crear(JornadaId, Limites(42)));

        await WhenAsync(comando);

        Then(PlantillaSemanalCreada.Crear(GuidAggregateId, NombrePlantilla, 2));
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, null);
    }
}
