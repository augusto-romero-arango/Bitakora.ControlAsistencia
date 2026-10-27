using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class AdvertenciaPlantillaTurnosIgualdadTests : IgualdadTestBase<AdvertenciaPlantillaTurnos>
{
    protected override AdvertenciaPlantillaTurnos CrearInstancia() =>
        AdvertenciaPlantillaTurnos.SuperaTopeDiario(3, 60);

    protected override AdvertenciaPlantillaTurnos CrearInstanciaCopia() =>
        AdvertenciaPlantillaTurnos.SuperaTopeDiario(3, 60);

    protected override IEnumerable<(string atributo, AdvertenciaPlantillaTurnos diferente)> CrearInstanciasDiferentes()
    {
        yield return ("Tipo", AdvertenciaPlantillaTurnos.PorDebajoDelMinimoDiario(3, 60));
        yield return ("Dia", AdvertenciaPlantillaTurnos.SuperaTopeDiario(4, 60));
        yield return ("Magnitud", AdvertenciaPlantillaTurnos.SuperaTopeDiario(3, 90));
    }
}
