using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class ModalidadJornada
{
    public static readonly ModalidadJornada Ordinaria = new("Ordinaria");
    public static readonly ModalidadJornada Flexible = new("Flexible");
    public static readonly ModalidadJornada TurnosSucesivos = new("TurnosSucesivos");
    public static readonly ModalidadJornada SinSolucionDeContinuidad = new("SinSolucionDeContinuidad");
    public static readonly ModalidadJornada ExcluidaDeJornadaMaxima = new("ExcluidaDeJornadaMaxima");

    private ModalidadJornada(string nombre) => Nombre = nombre;

    public string Nombre { get; private set; }

    public static ModalidadJornada Desde(string nombre) => throw new NotImplementedException();

    public static bool TryDesde(string? nombre, out ModalidadJornada? modalidad) => throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) => throw new NotImplementedException();

    public override string ToString() => throw new NotImplementedException();
}
