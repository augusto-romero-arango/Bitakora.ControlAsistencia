using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class LimitesJornada
{
    private HorasYMinutos _horasSemanales = null!;
    private HorasYMinutos _topeDiario = null!;
    private HorasYMinutos _minimoDiario = null!;
    private int _diasDescansoPorSemana;

    private LimitesJornada() { }

    public static LimitesJornada Crear(HorasYMinutos horasSemanales, HorasYMinutos topeDiario,
        HorasYMinutos minimoDiario, int diasDescansoPorSemana) => throw new NotImplementedException();
    public override string ToString() => throw new NotImplementedException();
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) => throw new NotImplementedException();
}
