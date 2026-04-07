using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Contracts.ValueObjects;

// Segmento continuo de trabajo dentro de un turno.
// Contiene sub-franjas de descanso y extras.
// ADR-0015: sealed class con factory static, constructor privado, campos readonly.
public sealed class FranjaOrdinaria : FranjaTemporal, IEquatable<FranjaOrdinaria>
{
    private readonly List<SubFranja> _descansos;
    private readonly List<SubFranja> _extras;

    // Constructor real: usado por el factory
    // diaOffsetInicio siempre es 0 — la ordinaria empieza en el dia base
    private FranjaOrdinaria(TimeOnly horaInicio, TimeOnly horaFin, int diaOffsetFin,
        List<SubFranja> descansos, List<SubFranja> extras)
        : base(horaInicio, horaFin, diaOffsetInicio: 0, diaOffsetFin)
    {
        _descansos = descansos;
        _extras = extras;
    }

    // Constructor vacio para STJ/Marten
    private FranjaOrdinaria()
    {
        _descansos = [];
        _extras = [];
    }

    // CA-8: factory estatico
    // CA-7: rechaza InicioYFinIguales
    // CA-13: infiere offset +1 cuando fin < inicio
    // CA-14 a CA-16: valida que descansos y extras esten contenidos
    // CA-17 a CA-19: valida que descansos y extras no se solapen entre si
    public static FranjaOrdinaria Crear(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetFin = 0,
        IEnumerable<SubFranja>? descansos = null,
        IEnumerable<SubFranja>? extras = null)
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

        // Proyectar todas las hijas como FranjaTemporal para validaciones unificadas
        var hijas = listaDescansos.Cast<FranjaTemporal>().Concat(listaExtras).ToList();

        // CA-14 a CA-16: validar contencion de todas las hijas
        ValidarContencion(ordinaria, hijas);

        // CA-17 a CA-19: validar que no haya solapamiento entre hijas
        ValidarSolapamiento(hijas);

        return ordinaria;
    }

    // Conversion a DTO plano para eventos entre dominios
    public DetalleFranjaOrdinaria ToDetalle() => new(
        _horaInicio, _horaFin, _diaOffsetFin,
        _descansos.Select(d => d.ToDetalle()).ToList().AsReadOnly(),
        _extras.Select(e => e.ToDetalle()).ToList().AsReadOnly());

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
    private static void ValidarContencion(FranjaOrdinaria contenedor, List<FranjaTemporal> hijas)
    {
        var inicio = contenedor.MinutosAbsolutoInicio;
        var fin = contenedor.MinutosAbsolutoFin;

        if (hijas.Any(h => h.MinutosAbsolutoInicio < inicio || h.MinutosAbsolutoFin > fin))
            throw new ArgumentException(Mensajes.FranjaHijaFueraDeContenedor);
    }

    // Verifica que ningun par de franjas hijas se solapen
    // CA-18: fin exclusivo, contiguas no se solapan
    private static void ValidarSolapamiento(List<FranjaTemporal> hijas)
    {
        for (var i = 0; i < hijas.Count; i++)
            for (var j = i + 1; j < hijas.Count; j++)
                if (hijas[i].MinutosAbsolutoInicio < hijas[j].MinutosAbsolutoFin
                    && hijas[j].MinutosAbsolutoInicio < hijas[i].MinutosAbsolutoFin)
                    throw new ArgumentException(Mensajes.FranjasHijasSeSuperponen);
    }

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

            RegistrarCampo(typeInfo, "_horaInicio", "horaInicio", typeof(TimeOnly), typeof(FranjaTemporal));
            RegistrarCampo(typeInfo, "_horaFin", "horaFin", typeof(TimeOnly), typeof(FranjaTemporal));
            RegistrarCampo(typeInfo, "_diaOffsetInicio", "diaOffsetInicio", typeof(int), typeof(FranjaTemporal));
            RegistrarCampo(typeInfo, "_diaOffsetFin", "diaOffsetFin", typeof(int), typeof(FranjaTemporal));

            // Colecciones de hijas
            var fDescansos = typeof(FranjaOrdinaria)
                .GetField("_descansos", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var pDescansos = typeInfo.CreateJsonPropertyInfo(typeof(List<SubFranja>), "descansos");
            pDescansos.Get = obj => fDescansos.GetValue(obj)!;
            pDescansos.Set = (obj, val) => fDescansos.SetValue(obj, val);
            typeInfo.Properties.Add(pDescansos);

            var fExtras = typeof(FranjaOrdinaria)
                .GetField("_extras", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var pExtras = typeInfo.CreateJsonPropertyInfo(typeof(List<SubFranja>), "extras");
            pExtras.Get = obj => fExtras.GetValue(obj)!;
            pExtras.Set = (obj, val) => fExtras.SetValue(obj, val);
            typeInfo.Properties.Add(pExtras);
        });
    }
}
