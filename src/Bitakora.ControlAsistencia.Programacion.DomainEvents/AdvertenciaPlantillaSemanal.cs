using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public enum TipoAdvertenciaPlantilla
{
    PlantillaSinJornada,
    DiaSinTurno,
    SuperaTopeDiario,
    PorDebajoDelMinimoDiario,
    SuperaHorasSemanales,
    PorDebajoDeHorasSemanales,
    FaltanDiasDeDescanso,
    SobranDiasDeDescanso
}

public sealed class AdvertenciaPlantillaSemanal
    : IEquatable<AdvertenciaPlantillaSemanal>, IComparable<AdvertenciaPlantillaSemanal>
{
    private TipoAdvertenciaPlantilla _tipo;
    private int? _semana;
    private int? _dia;
    private int _magnitud;

    private AdvertenciaPlantillaSemanal()
    {
    }

    public TipoAdvertenciaPlantilla Tipo => _tipo;
    public int? Semana => _semana;
    public int? Dia => _dia;
    public int Magnitud => _magnitud;

    private static AdvertenciaPlantillaSemanal Nueva(
        TipoAdvertenciaPlantilla tipo, int? semana = null, DiaSemana? dia = null, int magnitud = 0) =>
        new() { _tipo = tipo, _semana = semana, _dia = dia?.Numero, _magnitud = magnitud };

    public static AdvertenciaPlantillaSemanal PlantillaSinJornada() =>
        Nueva(TipoAdvertenciaPlantilla.PlantillaSinJornada);

    public static AdvertenciaPlantillaSemanal DiaSinTurno(int semana, DiaSemana dia) =>
        Nueva(TipoAdvertenciaPlantilla.DiaSinTurno, semana, dia);

    public static AdvertenciaPlantillaSemanal SuperaTopeDiario(int semana, DiaSemana dia, int minutosDeMas) =>
        Nueva(TipoAdvertenciaPlantilla.SuperaTopeDiario, semana, dia, minutosDeMas);

    public static AdvertenciaPlantillaSemanal PorDebajoDelMinimoDiario(int semana, DiaSemana dia, int minutosDeMenos) =>
        Nueva(TipoAdvertenciaPlantilla.PorDebajoDelMinimoDiario, semana, dia, minutosDeMenos);

    public static AdvertenciaPlantillaSemanal SuperaHorasSemanales(int semana, int minutosDeMas) =>
        Nueva(TipoAdvertenciaPlantilla.SuperaHorasSemanales, semana, magnitud: minutosDeMas);

    public static AdvertenciaPlantillaSemanal PorDebajoDeHorasSemanales(int semana, int minutosDeMenos) =>
        Nueva(TipoAdvertenciaPlantilla.PorDebajoDeHorasSemanales, semana, magnitud: minutosDeMenos);

    public static AdvertenciaPlantillaSemanal FaltanDiasDeDescanso(int semana, int dias) =>
        Nueva(TipoAdvertenciaPlantilla.FaltanDiasDeDescanso, semana, magnitud: dias);

    public static AdvertenciaPlantillaSemanal SobranDiasDeDescanso(int semana, int dias) =>
        Nueva(TipoAdvertenciaPlantilla.SobranDiasDeDescanso, semana, magnitud: dias);

    // Orden determinista: plantilla, semana, dia ISO (las de semana antes que las de dia), tipo.
    public int CompareTo(AdvertenciaPlantillaSemanal? otra) =>
        otra is null ? 1 : Clave().CompareTo(otra.Clave());

    private (int, int, TipoAdvertenciaPlantilla) Clave() => (_semana ?? 0, _dia ?? 0, _tipo);

    public bool Equals(AdvertenciaPlantillaSemanal? otra) =>
        otra is not null && _tipo == otra._tipo && _semana == otra._semana
        && _dia == otra._dia && _magnitud == otra._magnitud;

    public override bool Equals(object? obj) => Equals(obj as AdvertenciaPlantillaSemanal);

    public override int GetHashCode() => HashCode.Combine(_tipo, _semana, _dia, _magnitud);

    public override string ToString() => $"{_tipo}({_semana}, {_dia}, {_magnitud})";

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var tipo = typeof(AdvertenciaPlantillaSemanal);
        var ctor = tipo.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != tipo || info.Kind != JsonTypeInfoKind.Object) return;
            info.CreateObject = () => (AdvertenciaPlantillaSemanal)ctor.Invoke(null);
            info.Properties.Clear();
            Agregar(nameof(_tipo), "Tipo", typeof(TipoAdvertenciaPlantilla));
            Agregar(nameof(_semana), "Semana", typeof(int?));
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
