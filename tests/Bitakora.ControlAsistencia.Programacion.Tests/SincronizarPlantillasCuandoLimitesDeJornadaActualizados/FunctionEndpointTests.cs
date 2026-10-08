using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoLimitesDeJornadaActualizados;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SincronizarPlantillasCuandoLimitesDeJornadaActualizados;

public class FunctionEndpointTests
{
    private static MethodInfo Run => typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!;

    [Fact]
    public void Run_SeLlamaSincronizarPlantillasCuandoLimitesDeJornadaActualizados_ComoFuncionDelWorker()
    {
        Run.GetCustomAttribute<FunctionAttribute>().Should().NotBeNull();
        Run.GetCustomAttribute<FunctionAttribute>()!.Name
            .Should().Be("SincronizarPlantillasCuandoLimitesDeJornadaActualizados");
    }

    [Fact]
    public void Run_SeSuscribeAlTopicDeLimitesConLaSuscripcionDeProgramacion_ConLaConexionDeServiceBus()
    {
        var trigger = Run.GetParameters()
            .Select(p => p.GetCustomAttribute<ServiceBusTriggerAttribute>())
            .Single(a => a is not null)!;

        trigger.TopicName.Should().Be("limites-de-jornada-actualizados");
        trigger.SubscriptionName.Should().Be("programacion-escucha-limites-de-jornada");
        trigger.Connection.Should().Be("SERVICE_BUS_CONNECTION");
    }
}
