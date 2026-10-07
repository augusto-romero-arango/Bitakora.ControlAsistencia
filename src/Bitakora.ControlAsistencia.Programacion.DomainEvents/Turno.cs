using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class Turno : IEquatable<Turno>
{
    private string _nombre = string.Empty;
    private bool _esDescanso;
    private List<FranjaOrdinaria> _franjas = [];

    private Turno()
    {
    }

    public static Turno Crear(string nombre, bool esDescanso, IEnumerable<FranjaOrdinaria> franjas) =>
        new() { _nombre = nombre, _esDescanso = esDescanso, _franjas = franjas.ToList() };

    public bool EsDescanso() => _esDescanso;

    public int MinutosOrdinarios() => throw new NotImplementedException();

    public bool EstaCompleto() => _esDescanso || _franjas.Count > 0;

    public TurnoProgramado Programar() => new(
        _nombre,
        _franjas.Select(f => f.ToDetalle()).ToList().AsReadOnly(),
        ToString());

    public FranjaOrdinaria? FranjaQueEmpiezaA(TimeOnly horaInicio) =>
        _franjas.FirstOrDefault(f => f.EmpiezaA(horaInicio));

    public bool SeSolapaCon(FranjaOrdinaria franja) => _franjas.Any(f => f.SeSolapaCon(franja));

    public Turno ConFranja(FranjaOrdinaria franja) => Crear(_nombre, _esDescanso, [.. _franjas, franja]);

    public Turno SinFranjaQueEmpiezaA(FranjaOrdinaria franja) =>
        Crear(_nombre, _esDescanso, _franjas.Where(f => !f.EmpiezaALaMismaHoraQue(franja)));

    public Turno ConFranjaReemplazada(FranjaOrdinaria franja) =>
        Crear(_nombre, _esDescanso,
            _franjas.Select(f => f.EmpiezaALaMismaHoraQue(franja) ? franja : f));

    public override string ToString() => (_esDescanso, _franjas.Count) switch
    {
        (true, _) => $"{_nombre} {Mensajes.LabelDescanso}",
        (false, 0) => $"{_nombre} {Mensajes.LabelIncompleto}",
        _ => $"{_nombre} {string.Join("", _franjas)}"
    };

    public bool Equals(Turno? other) =>
        other is not null
        && _nombre == other._nombre
        && _esDescanso == other._esDescanso
        && _franjas.SequenceEqual(other._franjas);

    public override bool Equals(object? obj) => Equals(obj as Turno);

    public override int GetHashCode() =>
        _franjas.Aggregate(HashCode.Combine(_nombre, _esDescanso), HashCode.Combine);

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(Turno)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(Turno)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (Turno)ctor.Invoke(null);

            Registrar<string>(typeInfo, "_nombre", "nombre");
            Registrar<bool>(typeInfo, "_esDescanso", "esDescanso");
            Registrar<List<FranjaOrdinaria>>(typeInfo, "_franjas", "franjas");
        });
    }

    private static void Registrar<T>(JsonTypeInfo typeInfo, string campo, string nombreJson)
    {
        var field = typeof(Turno).GetField(campo, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var prop = typeInfo.CreateJsonPropertyInfo(typeof(T), nombreJson);
        prop.Get = obj => field.GetValue(obj)!;
        prop.Set = (obj, val) => field.SetValue(obj, val);
        typeInfo.Properties.Add(prop);
    }
}
