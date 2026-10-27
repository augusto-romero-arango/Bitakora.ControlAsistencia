using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class AdvertenciaPlantillaTurnos
    : IEquatable<AdvertenciaPlantillaTurnos>, IComparable<AdvertenciaPlantillaTurnos>
{
    private AdvertenciaPlantillaTurnos()
    {
    }

    public TipoAdvertenciaPlantilla Tipo => throw new NotImplementedException();
    public int? Dia => throw new NotImplementedException();
    public int Magnitud => throw new NotImplementedException();

    public static AdvertenciaPlantillaTurnos PlantillaSinJornada() => throw new NotImplementedException();

    public static AdvertenciaPlantillaTurnos DiaSinTurno(int dia) => throw new NotImplementedException();

    public static AdvertenciaPlantillaTurnos SuperaTopeDiario(int dia, int minutosDeMas) =>
        throw new NotImplementedException();

    public static AdvertenciaPlantillaTurnos PorDebajoDelMinimoDiario(int dia, int minutosDeMenos) =>
        throw new NotImplementedException();

    public int CompareTo(AdvertenciaPlantillaTurnos? otra) => throw new NotImplementedException();

    public bool Equals(AdvertenciaPlantillaTurnos? otra) => throw new NotImplementedException();

    public override bool Equals(object? obj) => Equals(obj as AdvertenciaPlantillaTurnos);

    public override int GetHashCode() => throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
    }
}
