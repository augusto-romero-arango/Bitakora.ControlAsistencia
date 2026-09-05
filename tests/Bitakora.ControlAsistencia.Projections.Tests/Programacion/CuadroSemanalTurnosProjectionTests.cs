// Invocacion DIRECTA de los metodos estaticos, no el DSL Given/When/Then de
// CommandHandlerTestBase: aqui se prueba una funcion pura evento -> vista, sin abrir ningun stream.
//
// Cada oraculo se arma a mano (MEF-ADR-0002, no-tautologia): las vistas previas y las esperadas se
// construyen con el constructor posicional del record, nunca reusando la logica del SUT.
//
// BeEquivalentTo, no Be: CuadroSemanalTurnos es un record plano sin igualdad por valor sobre su
// coleccion Dias.
//
// Sin test para "Apply de un evento de dia sobre un stream sin creacion": esa garantia es
// estructural -- la clase no declara ningun Create para esos eventos, y el dispatcher generado no
// materializa nada sin un Create previo. Un test sobre esa ausencia seria tautologico.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events;

namespace Bitakora.ControlAsistencia.Projections.Tests.Programacion;

public class CuadroSemanalTurnosProjectionTests
{
    // CA-1: el PlantillaId embebido en el evento se fija DISTINTO del StreamKey a proposito -- un
    // Create que leyera e.Data.PlantillaId.ToString() en vez de e.StreamKey quedaria en evidencia.
    [Fact]
    public void Create_ProyectaElCuadroVacio_DesdePlantillaSemanalCreada()
    {
        var plantillaIdDelPayload = Guid.Parse("019600b0-0000-7000-8000-000000000099");
        var plantillaCreada = PlantillaSemanalCreada.Crear(plantillaIdDelPayload, "Semana Cocina", 2);
        var evento = new Event<PlantillaSemanalCreada>(plantillaCreada)
        {
            StreamKey = "plantilla-001",
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var vista = CuadroSemanalTurnosProjection.Create(evento);

        vista.Should().BeEquivalentTo(new CuadroSemanalTurnos("plantilla-001", "Semana Cocina", 2, []));
    }

    // CA-2. TurnoId es turnoId.ToString() (formato "D", minusculas): construir el esperado con la
    // misma llamada es el valor de dato de la fixture, no la logica del SUT.
    [Fact]
    public void Apply_AgregaElDia_CuandoDiaDePlantillaSemanalAsignadoSobreCuadroVacio()
    {
        var plantillaId = Guid.NewGuid();
        var turnoId = Guid.Parse("019600b0-0000-7000-8000-000000000001");
        var cuadroVacio = new CuadroSemanalTurnos(plantillaId.ToString(), "Semana Cocina", 2, []);

        var evento = DiaDePlantillaSemanalAsignado.Crear(plantillaId, 1, DiaSemana.Desde(5), turnoId);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, cuadroVacio);

        vista.Dias.Should().BeEquivalentTo([new DiaDelCuadro(1, 5, turnoId.ToString())]);
    }

    // CA-2: reasignar el MISMO slot reemplaza el turno, no agrega un segundo elemento.
    [Fact]
    public void Apply_ReemplazaElDia_CuandoDiaDePlantillaSemanalAsignadoSobreElMismoSlot()
    {
        var plantillaId = Guid.NewGuid();
        var turnoId1 = Guid.Parse("019600b0-0000-7000-8000-000000000001");
        var turnoId2 = Guid.Parse("019600b0-0000-7000-8000-000000000002");
        var cuadroConUnDia = new CuadroSemanalTurnos(
            plantillaId.ToString(), "Semana Cocina", 2,
            [new DiaDelCuadro(1, 5, turnoId1.ToString())]);

        var evento = DiaDePlantillaSemanalAsignado.Crear(plantillaId, 1, DiaSemana.Desde(5), turnoId2);

        var vista = CuadroSemanalTurnosProjection.Apply(evento, cuadroConUnDia);

        vista.Dias.Should().BeEquivalentTo([new DiaDelCuadro(1, 5, turnoId2.ToString())]);
    }

    // CA-2: la vista se lee lunes -> domingo (MEF-ADR-0041), no en el orden de asignacion.
    [Fact]
    public void Apply_OrdenaLosDiasPorSemanaYDia_CuandoSeAsignanVariosSlotsDesordenados()
    {
        var plantillaId = Guid.NewGuid();
        var turnoId2 = Guid.Parse("019600b0-0000-7000-8000-000000000002");
        var turnoId3 = Guid.Parse("019600b0-0000-7000-8000-000000000003");
        var turnoId4 = Guid.Parse("019600b0-0000-7000-8000-000000000004");
        var cuadroPrevio = new CuadroSemanalTurnos(
            plantillaId.ToString(), "Semana Cocina", 2,
            [new DiaDelCuadro(1, 5, turnoId2.ToString())]);

        var vistaTrasSemana2 = CuadroSemanalTurnosProjection.Apply(
            DiaDePlantillaSemanalAsignado.Crear(plantillaId, 2, DiaSemana.Desde(1), turnoId3),
            cuadroPrevio);
        var vistaFinal = CuadroSemanalTurnosProjection.Apply(
            DiaDePlantillaSemanalAsignado.Crear(plantillaId, 1, DiaSemana.Desde(7), turnoId4),
            vistaTrasSemana2);

        vistaFinal.Dias.Should().BeEquivalentTo(
            [
                new DiaDelCuadro(1, 5, turnoId2.ToString()),
                new DiaDelCuadro(1, 7, turnoId4.ToString()),
                new DiaDelCuadro(2, 1, turnoId3.ToString()),
            ],
            opciones => opciones.WithStrictOrdering());
    }

    // CA-3.
    [Fact]
    public void Apply_QuitaElDiaCuyoSlotCoincide_CuandoDiaDePlantillaSemanalQuitado()
    {
        var plantillaId = Guid.NewGuid();
        var turnoId5 = Guid.Parse("019600b0-0000-7000-8000-000000000005");
        var turnoId6 = Guid.Parse("019600b0-0000-7000-8000-000000000006");
        var cuadroConDosDias = new CuadroSemanalTurnos(
            plantillaId.ToString(), "Semana Cocina", 2,
            [
                new DiaDelCuadro(1, 5, turnoId5.ToString()),
                new DiaDelCuadro(1, 6, turnoId6.ToString()),
            ]);

        var evento = DiaDePlantillaSemanalQuitado.Crear(plantillaId, 1, DiaSemana.Desde(5));

        var vista = CuadroSemanalTurnosProjection.Apply(evento, cuadroConDosDias);

        vista.Dias.Should().BeEquivalentTo([new DiaDelCuadro(1, 6, turnoId6.ToString())]);
    }

    // CA-3: Apply nunca lanza (MEF-ADR-0004 capa 4) -- quitar un slot ausente deja la vista igual.
    [Fact]
    public void Apply_DejaLaVistaSinCambios_CuandoDiaDePlantillaSemanalQuitadoSobreSlotAusente()
    {
        var plantillaId = Guid.NewGuid();
        var turnoId6 = Guid.Parse("019600b0-0000-7000-8000-000000000006");
        var cuadroSinEseSlot = new CuadroSemanalTurnos(
            plantillaId.ToString(), "Semana Cocina", 2,
            [new DiaDelCuadro(1, 6, turnoId6.ToString())]);

        var evento = DiaDePlantillaSemanalQuitado.Crear(plantillaId, 1, DiaSemana.Desde(5));

        var vista = CuadroSemanalTurnosProjection.Apply(evento, cuadroSinEseSlot);

        vista.Should().BeEquivalentTo(cuadroSinEseSlot);
    }

    // CA-4: el retiro borra el cuadro -- la memoria queda en el stream y el nombre queda libre
    // para reusarse (CA-ADR-0034 decision 4).
    [Fact]
    public void ShouldDelete_BorraElCuadro_CuandoPlantillaSemanalRetirada()
    {
        var evento = PlantillaSemanalRetirada.Crear(Guid.NewGuid());

        var debeBorrarse = CuadroSemanalTurnosProjection.ShouldDelete(evento);

        debeBorrarse.Should().BeTrue();
    }
}
