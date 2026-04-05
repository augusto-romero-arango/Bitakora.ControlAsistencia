using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Segmento continuo de trabajo dentro de un turno.
// Puede contener FranjaDescanso y FranjaExtra.
// ADR-0015: sealed class con factory static, constructor privado, campos readonly.
public sealed class FranjaOrdinaria : FranjaTemporal, IEquatable<FranjaOrdinaria>
{
    // CA-4: offset del fin respecto al dia de inicio
    private readonly int _diaOffsetFin;
    private readonly List<FranjaDescanso> _descansos;
    private readonly List<FranjaExtra> _extras;

    // Constructor real: usado por el factory
    private FranjaOrdinaria(TimeOnly horaInicio, TimeOnly horaFin, int diaOffsetFin,
        List<FranjaDescanso> descansos, List<FranjaExtra> extras)
        : base(horaInicio, horaFin)
    {
        _diaOffsetFin = diaOffsetFin;
        _descansos = descansos;
        _extras = extras;
    }

    // Constructor vacio para STJ/Marten
    private FranjaOrdinaria()
    {
        _descansos = [];
        _extras = [];
    }

    internal override int MinutosAbsolutoInicio => CalcularMinutosAbsolutos(_horaInicio, 0);
    internal override int MinutosAbsolutoFin => CalcularMinutosAbsolutos(_horaFin, _diaOffsetFin);

    // CA-8: factory estatico
    // CA-7: rechaza InicioYFinIguales
    // CA-13: infiere offset +1 cuando fin < inicio
    // CA-14 a CA-16: valida que descansos y extras esten contenidos
    // CA-17 a CA-19: valida que descansos y extras no se solapen entre si
    public static FranjaOrdinaria Crear(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetFin = 0,
        IEnumerable<FranjaDescanso>? descansos = null,
        IEnumerable<FranjaExtra>? extras = null)
    {
        // CA-13: inferir offset cuando fin < inicio y no se especifico
        if (diaOffsetFin == 0 && horaFin < horaInicio)
            diaOffsetFin = 1;

        // CA-7: rechazar duracion cero
        if (horaInicio == horaFin && diaOffsetFin == 0)
            throw new ArgumentException(Mensajes.InicioYFinIguales);

        var listaDescansos = descansos?.ToList() ?? [];
        var listaExtras = extras?.ToList() ?? [];

        var ordinaria = new FranjaOrdinaria(horaInicio, horaFin, diaOffsetFin,
            listaDescansos, listaExtras);

        // CA-14 a CA-16: validar contencion de todas las hijas
        ValidarContencion(ordinaria, listaDescansos, listaExtras);

        // CA-17 a CA-19: validar que no haya solapamiento entre hijas
        ValidarSolapamiento(listaDescansos, listaExtras);

        return ordinaria;
    }

    // CA-20, CA-21: formato "(06:00-12:00)" o "(22:00-06:00+1)"
    public override string ToString()
    {
        var resultado = $"({FormatearHora(_horaInicio, 0)}-{FormatearHora(_horaFin, _diaOffsetFin)})";

        if (_descansos.Count > 0)
            resultado += $"[{Mensajes.LabelDescansos}:{string.Join(", ", _descansos)}]";

        if (_extras.Count > 0)
            resultado += $"[{Mensajes.LabelExtras}:{string.Join(", ", _extras)}]";

        return resultado;
    }

    // Igualdad por valor
    public bool Equals(FranjaOrdinaria? other)
    {
        if (other is null) return false;
        if (_horaInicio != other._horaInicio || _horaFin != other._horaFin
            || _diaOffsetFin != other._diaOffsetFin) return false;
        return _descansos.SequenceEqual(other._descansos)
            && _extras.SequenceEqual(other._extras);
    }

    public override bool Equals(object? obj) => Equals(obj as FranjaOrdinaria);

    public override int GetHashCode()
    {
        var hash = HashCode.Combine(_horaInicio, _horaFin, _diaOffsetFin);
        foreach (var d in _descansos) hash = HashCode.Combine(hash, d);
        foreach (var e in _extras) hash = HashCode.Combine(hash, e);
        return hash;
    }

    // Verifica que cada franja hija este contenida dentro de la ordinaria
    private static void ValidarContencion(
        FranjaOrdinaria contenedor,
        List<FranjaDescanso> descansos,
        List<FranjaExtra> extras)
    {
        var inicioContenedor = contenedor.MinutosAbsolutoInicio;
        var finContenedor = contenedor.MinutosAbsolutoFin;

        foreach (var descanso in descansos)
        {
            if (!EstaContenida(inicioContenedor, finContenedor,
                    descanso.MinutosAbsolutoInicio, descanso.MinutosAbsolutoFin))
                throw new ArgumentException(Mensajes.FranjaHijaFueraDeContenedor);
        }

        foreach (var extra in extras)
        {
            if (!EstaContenida(inicioContenedor, finContenedor,
                    extra.MinutosAbsolutoInicio, extra.MinutosAbsolutoFin))
                throw new ArgumentException(Mensajes.FranjaHijaFueraDeContenedor);
        }
    }

    // Contencion: hijo debe empezar >= padre inicio y terminar <= padre fin
    private static bool EstaContenida(int inicioContenedor, int finContenedor,
        int inicioHija, int finHija) =>
        inicioHija >= inicioContenedor && finHija <= finContenedor;

    // Verifica que ningun par de franjas hijas se solapen
    private static void ValidarSolapamiento(
        List<FranjaDescanso> descansos,
        List<FranjaExtra> extras)
    {
        // Convertir todas las hijas a tuplas (inicio, fin) en minutos absolutos
        var rangos = descansos
            .Select(d => (Inicio: d.MinutosAbsolutoInicio, Fin: d.MinutosAbsolutoFin))
            .Concat(extras.Select(e => (Inicio: e.MinutosAbsolutoInicio, Fin: e.MinutosAbsolutoFin)))
            .ToList();

        // Verificar cada par - CA-18: fin exclusivo, contiguas no se solapan
        for (var i = 0; i < rangos.Count; i++)
        {
            for (var j = i + 1; j < rangos.Count; j++)
            {
                if (SeSolapan(rangos[i], rangos[j]))
                    throw new ArgumentException(Mensajes.FranjasHijasSeSuperponen);
            }
        }
    }

    // Dos rangos se solapan si uno empieza antes de que el otro termine (fin exclusivo)
    private static bool SeSolapan((int Inicio, int Fin) a, (int Inicio, int Fin) b) =>
        a.Inicio < b.Fin && b.Inicio < a.Fin;

    // Mapping de serializacion - vive aqui porque cambia con la clase
    internal static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(FranjaOrdinaria)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(FranjaOrdinaria)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (FranjaOrdinaria)ctor.Invoke(null);

            RegistrarCampo(typeInfo, "_horaInicio", "horaInicio", typeof(TimeOnly), typeof(FranjaOrdinaria));
            RegistrarCampo(typeInfo, "_horaFin", "horaFin", typeof(TimeOnly), typeof(FranjaOrdinaria));
            RegistrarCampo(typeInfo, "_diaOffsetFin", "diaOffsetFin", typeof(int), typeof(FranjaOrdinaria));

            // Colecciones de hijas
            var fDescansos = typeof(FranjaOrdinaria)
                .GetField("_descansos", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var pDescansos = typeInfo.CreateJsonPropertyInfo(typeof(List<FranjaDescanso>), "descansos");
            pDescansos.Get = obj => fDescansos.GetValue(obj)!;
            pDescansos.Set = (obj, val) => fDescansos.SetValue(obj, val);
            typeInfo.Properties.Add(pDescansos);

            var fExtras = typeof(FranjaOrdinaria)
                .GetField("_extras", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var pExtras = typeInfo.CreateJsonPropertyInfo(typeof(List<FranjaExtra>), "extras");
            pExtras.Get = obj => fExtras.GetValue(obj)!;
            pExtras.Set = (obj, val) => fExtras.SetValue(obj, val);
            typeInfo.Properties.Add(pExtras);
        });
    }
}
