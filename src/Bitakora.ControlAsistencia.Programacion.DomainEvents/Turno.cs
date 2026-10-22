using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class Turno : IEquatable<Turno>
{
    private string _nombre = string.Empty;
    private bool _esDescanso;
    private List<FranjaOrdinaria> _franjas = [];
    private HorasYMinutos _amplitudVentanaEntrada = HorasYMinutos.Crear(0, 0);

    // Espejo de TraslapeNocturno.HoraCorte de ControlHoras: Programacion no puede referenciarlo.
    private static readonly TimeOnly HoraCorte = new(4, 0);

    private Turno()
    {
    }

    public static Turno Crear(string nombre, bool esDescanso, IEnumerable<FranjaOrdinaria> franjas) =>
        new() { _nombre = nombre, _esDescanso = esDescanso, _franjas = franjas.ToList() };

    public string Nombre => _nombre;

    public bool EsDescanso() => _esDescanso;

    public int MinutosOrdinarios() => _esDescanso ? 0 : _franjas.Sum(f => f.MinutosOrdinarios());

    public bool EstaCompleto() => _esDescanso || _franjas.Count > 0;

    public TurnoProgramado Programar() => new(
        _nombre,
        _franjas.Select(f => f.ToDetalle()).ToList().AsReadOnly(),
        ToString(),
        _amplitudVentanaEntrada.TotalMinutos());

    public FranjaOrdinaria? FranjaQueEmpiezaA(TimeOnly horaInicio) =>
        _franjas.FirstOrDefault(f => f.EmpiezaA(horaInicio));

    public bool SeSolapaCon(FranjaOrdinaria franja) => _franjas.Any(f => f.SeSolapaCon(franja));

    public Turno ConFranja(FranjaOrdinaria franja) => Copiar(franjas: [.. _franjas, franja]);

    public Turno SinFranjaQueEmpiezaA(FranjaOrdinaria franja) =>
        Copiar(franjas: _franjas.Where(f => !f.EmpiezaALaMismaHoraQue(franja)));

    public Turno ConFranjaReemplazada(FranjaOrdinaria franja) =>
        Copiar(franjas: _franjas.Select(f => f.EmpiezaALaMismaHoraQue(franja) ? franja : f));

    public EvaluacionVentanaDeEntrada EvaluarVentanaDeEntrada() => throw new NotImplementedException();

    public EvaluacionVentanaDeEntrada EvaluarVentanaDeEntrada(HorasYMinutos amplitud)
    {
        if (amplitud.TotalMinutos() == 0)
            return EvaluacionVentanaDeEntrada.Valida;

        if (_esDescanso || _franjas.Count == 0)
            return EvaluacionVentanaDeEntrada.TurnoSinFranjas;

        if (_franjas.Any(f => f.EmpiezaAntesDe(HoraCorte)))
            return EvaluacionVentanaDeEntrada.InicioAntesDeHoraCorte;

        return _franjas.Any(f => f.LlegaALaMedianocheDesplazadaEn(amplitud))
            ? EvaluacionVentanaDeEntrada.CierrePasaMedianoche
            : EvaluacionVentanaDeEntrada.Valida;
    }

    public bool TieneVentanaDeEntrada(HorasYMinutos amplitud) => _amplitudVentanaEntrada.Equals(amplitud);

    public Turno ConVentanaDeEntrada(HorasYMinutos amplitud) => Copiar(amplitud: amplitud);

    private Turno Copiar(IEnumerable<FranjaOrdinaria>? franjas = null, HorasYMinutos? amplitud = null) =>
        new()
        {
            _nombre = _nombre,
            _esDescanso = _esDescanso,
            _franjas = (franjas ?? _franjas).ToList(),
            _amplitudVentanaEntrada = amplitud ?? _amplitudVentanaEntrada
        };

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
        && _amplitudVentanaEntrada.Equals(other._amplitudVentanaEntrada)
        && _franjas.SequenceEqual(other._franjas);

    public override bool Equals(object? obj) => Equals(obj as Turno);

    public override int GetHashCode() =>
        _franjas.Aggregate(HashCode.Combine(_nombre, _esDescanso, _amplitudVentanaEntrada), HashCode.Combine);

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
            Registrar<HorasYMinutos>(typeInfo, "_amplitudVentanaEntrada", "amplitudVentanaEntrada");
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
