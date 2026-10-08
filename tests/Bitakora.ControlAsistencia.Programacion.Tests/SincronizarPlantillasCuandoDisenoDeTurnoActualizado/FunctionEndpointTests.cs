using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoDisenoDeTurnoActualizado;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SincronizarPlantillasCuandoDisenoDeTurnoActualizado;

public class FunctionEndpointTests
{
    private static MethodInfo Run => typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;

    [Fact]
    public void Run_SeLlamaSincronizarPlantillasCuandoDisenoDeTurnoActualizado_ComoFuncionDelWorker()
    {
        Run.GetCustomAttribute<FunctionAttribute>().Should().NotBeNull();
        Run.GetCustomAttribute<FunctionAttribute>()!.Name
            .Should().Be("SincronizarPlantillasCuandoDisenoDeTurnoActualizado");
    }

    [Fact]
    public void Run_SeSuscribeAlTopicDelDisenoConLaSuscripcionDeProgramacion_ConLaConexionDeServiceBus()
    {
        var trigger = Run.GetParameters()
            .Select(p => p.GetCustomAttribute<ServiceBusTriggerAttribute>())
            .Single(a => a is not null)!;

        trigger.TopicName.Should().Be("diseno-de-turno-actualizado");
        trigger.SubscriptionName.Should().Be("programacion-escucha-diseno-de-turno");
        trigger.Connection.Should().Be("SERVICE_BUS_CONNECTION");
    }
}
