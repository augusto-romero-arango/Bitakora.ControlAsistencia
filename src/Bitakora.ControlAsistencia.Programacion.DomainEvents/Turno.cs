using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class Turno : IEquatable<Turno>
{
    private Turno()
    {
    }

    public static Turno Crear(string nombre, bool esDescanso, IEnumerable<FranjaOrdinaria> franjas)
        => throw new NotImplementedException();

    public bool EstaCompleto() => throw new NotImplementedException();

    public TurnoProgramado Programar() => throw new NotImplementedException();

    public FranjaOrdinaria? FranjaQueEmpiezaA(TimeOnly horaInicio) => throw new NotImplementedException();

    public bool SeSolapaCon(FranjaOrdinaria franja) => throw new NotImplementedException();

    public Turno ConFranja(FranjaOrdinaria franja) => throw new NotImplementedException();

    public Turno SinFranjaQueEmpiezaA(FranjaOrdinaria franja) => throw new NotImplementedException();

    public Turno ConFranjaReemplazada(FranjaOrdinaria franja) => throw new NotImplementedException();

    public override string ToString() => throw new NotImplementedException();

    public bool Equals(Turno? other) => throw new NotImplementedException();

    public override bool Equals(object? obj) => throw new NotImplementedException();

    public override int GetHashCode() => throw new NotImplementedException();

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
        => throw new NotImplementedException();
}
