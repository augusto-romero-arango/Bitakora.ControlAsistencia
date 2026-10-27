using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed class AdvertenciaPlantillaTurnos
    : IEquatable<AdvertenciaPlantillaTurnos>, IComparable<AdvertenciaPlantillaTurnos>
{
    private TipoAdvertenciaPlantilla _tipo;
    private int? _dia;
    private int _magnitud;

    private AdvertenciaPlantillaTurnos()
    {
    }

    public TipoAdvertenciaPlantilla Tipo => _tipo;
    public int? Dia => _dia;
    public int Magnitud => _magnitud;

    private static AdvertenciaPlantillaTurnos Nueva(TipoAdvertenciaPlantilla tipo, int? dia = null, int magnitud = 0) =>
        new() { _tipo = tipo, _dia = dia, _magnitud = magnitud };

    public static AdvertenciaPlantillaTurnos PlantillaSinJornada() =>
        Nueva(TipoAdvertenciaPlantilla.PlantillaSinJornada);

    public static AdvertenciaPlantillaTurnos DiaSinTurno(int dia) =>
        Nueva(TipoAdvertenciaPlantilla.DiaSinTurno, dia);

    public static AdvertenciaPlantillaTurnos SuperaTopeDiario(int dia, int minutosDeMas) =>
        Nueva(TipoAdvertenciaPlantilla.SuperaTopeDiario, dia, minutosDeMas);

    public static AdvertenciaPlantillaTurnos PorDebajoDelMinimoDiario(int dia, int minutosDeMenos) =>
        Nueva(TipoAdvertenciaPlantilla.PorDebajoDelMinimoDiario, dia, minutosDeMenos);

    // Orden determinista: la de plantilla (sin dia) primero, luego dia y tipo.
    public int CompareTo(AdvertenciaPlantillaTurnos? otra) =>
        otra is null ? 1 : Clave().CompareTo(otra.Clave());

    private (int, TipoAdvertenciaPlantilla) Clave() => (_dia ?? 0, _tipo);

    public bool Equals(AdvertenciaPlantillaTurnos? otra) =>
        otra is not null && _tipo == otra._tipo && _dia == otra._dia && _magnitud == otra._magnitud;

    public override bool Equals(object? obj) => Equals(obj as AdvertenciaPlantillaTurnos);

    public override int GetHashCode() => HashCode.Combine(_tipo, _dia, _magnitud);

    public override string ToString() => $"{_tipo}({_dia}, {_magnitud})";

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(AdvertenciaPlantillaTurnos);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (AdvertenciaPlantillaTurnos)ctor.Invoke(null);
            info.Properties.Clear();
            Agregar(nameof(_tipo), "Tipo", typeof(TipoAdvertenciaPlantilla));
            Agregar(nameof(_dia), "Dia", typeof(int?));
            Agregar(nameof(_magnitud), "Magnitud", typeof(int));

            void Agregar(string nombreCampo, string nombreJson, Type tipoCampo)
            {
                var campo = tipo.GetField(nombreCampo, BindingFlags.NonPublic | BindingFlags.Instance)!;
                var propiedad = info.CreateJsonPropertyInfo(tipoCampo, nombreJson);
                propiedad.Get = obj => campo.GetValue(obj);
                propiedad.Set = (obj, valor) => campo.SetValue(obj, valor);
                info.Properties.Add(propiedad);
            }
        });
    }
}
