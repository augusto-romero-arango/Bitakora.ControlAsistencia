using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class AdvertenciaPlantillaSemanal
{
    private AdvertenciaPlantillaSemanal()
    {
    }

    public static AdvertenciaPlantillaSemanal PlantillaSinJornada() => throw new NotImplementedException();

    public static AdvertenciaPlantillaSemanal DiaSinTurno(int semana, DiaSemana dia) =>
        throw new NotImplementedException();

    public static AdvertenciaPlantillaSemanal SuperaTopeDiario(int semana, DiaSemana dia, int minutosDeMas) =>
        throw new NotImplementedException();

    public static AdvertenciaPlantillaSemanal PorDebajoDelMinimoDiario(int semana, DiaSemana dia, int minutosDeMenos) =>
        throw new NotImplementedException();

    public static AdvertenciaPlantillaSemanal SuperaHorasSemanales(int semana, int minutosDeMas) =>
        throw new NotImplementedException();

    public static AdvertenciaPlantillaSemanal PorDebajoDeHorasSemanales(int semana, int minutosDeMenos) =>
        throw new NotImplementedException();

    public static AdvertenciaPlantillaSemanal FaltanDiasDeDescanso(int semana, int dias) =>
        throw new NotImplementedException();

    public static AdvertenciaPlantillaSemanal SobranDiasDeDescanso(int semana, int dias) =>
        throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
    }
}
