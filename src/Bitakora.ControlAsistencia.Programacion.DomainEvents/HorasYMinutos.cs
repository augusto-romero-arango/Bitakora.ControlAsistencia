using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class HorasYMinutos : IComparable<HorasYMinutos>
{
    private int _minutosTotales;

    private HorasYMinutos() { }

    public static HorasYMinutos Crear(int horas, int minutos) => throw new NotImplementedException();
    public int CompareTo(HorasYMinutos? otro) => throw new NotImplementedException();
    public HorasYMinutos Por(int factor) => throw new NotImplementedException();
    public override string ToString() => throw new NotImplementedException();
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) => throw new NotImplementedException();
}
