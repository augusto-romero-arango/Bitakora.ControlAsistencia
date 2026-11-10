using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

public sealed partial class FranjaOrdinaria : FranjaTemporal, IEquatable<FranjaOrdinaria>
{
    private readonly List<SubFranja> _descansos;
    private readonly List<SubFranja> _extras;

    // Sede prearmada del catalogo (null = sin sede). Sin propiedad publica (MEF-ADR-0012).
    private readonly SedeProgramada? _sede;

    private FranjaOrdinaria(TimeOnly horaInicio, TimeOnly horaFin, int diaOffsetFin,
        List<SubFranja> descansos, List<SubFranja> extras, SedeProgramada? sede)
        : base(horaInicio, horaFin, diaOffsetInicio: 0, diaOffsetFin)
    {
        _descansos = descansos;
        _extras = extras;
        _sede = sede;
    }

    // Constructor vacio para STJ/Marten
    private FranjaOrdinaria()
    {
        _descansos = [];
        _extras = [];
    }

    // Duracion en (0, 24 h]: 24 h exactas se acepta para esquemas 24x24. La sede se valida aqui, junto
    // a las demas invariantes de la franja, y no en el RequestValidator del comando.
    public static FranjaOrdinaria Crear(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetFin = 0,
        IEnumerable<SubFranja>? descansos = null,
        IEnumerable<SubFranja>? extras = null,
        SedeProgramada? sede = null)
    {
        if (diaOffsetFin == 0 && horaFin < horaInicio)
            diaOffsetFin = 1;

        if (sede is not null && !sede.EstaCompleta())
            throw new ArgumentException(Mensajes.SedeIncompleta);

        var listaDescansos = descansos?.ToList() ?? [];
        var listaExtras = extras?.ToList() ?? [];

        var ordinaria = new FranjaOrdinaria(horaInicio, horaFin, diaOffsetFin,
            listaDescansos, listaExtras, sede);

        var duracionEnMinutos = ordinaria.DuracionEnMinutos();
        if (duracionEnMinutos <= 0)
            throw new ArgumentException(FranjaTemporal.Mensajes.DuracionNoPositiva);
        if (duracionEnMinutos > MinutosPorDia)
            throw new ArgumentException(Mensajes.DuracionExcedeUnDia);

        var hijas = listaDescansos.Cast<FranjaTemporal>().Concat(listaExtras).ToList();

        ValidarContencion(ordinaria, hijas);

        ValidarSolapamiento(hijas);

        return ordinaria;
    }

    // Publica porque el aggregate vive en otro ensamblado (Programacion FA) y no alcanza
    // FranjaTemporal.SeSolapaCon, internal aqui. Fin exclusivo: contiguas no se solapan.
    public bool SeSolapaCon(FranjaOrdinaria otra) => ((FranjaTemporal)this).SeSolapaCon(otra);

    // Apply(DescansoAgregado)/Apply(ExtraAgregado) reciben la franja RESULTANTE y localizan con
    // esto cual reemplazar, sin que _horaInicio deje de ser privado.
    public bool EmpiezaALaMismaHoraQue(FranjaOrdinaria otra) => EmpiezaA(otra._horaInicio);

    // Contraparte de ConDescanso/ConExtra: null cuando ninguna hija de ese tipo empieza a esa
    // hora -- el aggregate nunca lee _descansos/_extras para averiguarlo (MEF-ADR-0012).
    public FranjaOrdinaria? SinDescanso(TimeOnly horaInicio) =>
        _descansos.Any(d => d.EmpiezaA(horaInicio))
            ? Crear(_horaInicio, _horaFin, _diaOffsetFin,
                _descansos.Where(d => !d.EmpiezaA(horaInicio)), _extras, _sede)
            : null;

    public FranjaOrdinaria? SinExtra(TimeOnly horaInicio) =>
        _extras.Any(e => e.EmpiezaA(horaInicio))
            ? Crear(_horaInicio, _horaFin, _diaOffsetFin,
                _descansos, _extras.Where(e => !e.EmpiezaA(horaInicio)), _sede)
            : null;

    public FranjaOrdinaria ConDescanso(TimeOnly inicio, TimeOnly fin) =>
        Crear(_horaInicio, _horaFin, _diaOffsetFin,
            [.. _descansos, InferirHija(inicio, fin)], _extras, _sede);

    public FranjaOrdinaria ConExtra(TimeOnly inicio, TimeOnly fin) =>
        Crear(_horaInicio, _horaFin, _diaOffsetFin,
            _descansos, [.. _extras, InferirHija(inicio, fin)], _sede);

    // Delega en Crear(), asi que hereda su validacion: con una sede nueva incompleta lanza
    // SedeIncompleta en vez de devolver la franja.
    public FranjaOrdinaria ConSede(SedeProgramada? sede) =>
        Crear(_horaInicio, _horaFin, _diaOffsetFin, _descansos, _extras, sede);

    // El aggregate lo pregunta para decidir el rechazo "nada que retirar" (FranjaSinSede) sin leer el VO.
    public bool TieneSedePrearmada() => _sede is not null;

    // Pregunta de la ventana de entrada del Turno: la ordinaria empieza siempre en offset 0.
    internal bool LlegaALaMedianocheDesplazadaEn(HorasYMinutos amplitud) =>
        _horaInicio.ToTimeSpan().TotalMinutes + amplitud.TotalMinutos() >= MinutosPorDia;

    public int MinutosOrdinarios() =>
        DuracionEnMinutos() - _descansos.Sum(d => d.DuracionEnMinutos()) - _extras.Sum(e => e.DuracionEnMinutos());

    // El dia de la hija es deducible solo porque la ordinaria esta acotada a <= 24 h: dentro de
    // ella cada HH:mm tiene un unico representante. Ampliar ese tope volveria ambigua esta
    // inferencia.
    private SubFranja InferirHija(TimeOnly inicio, TimeOnly fin)
    {
        var offsetInicio = inicio < _horaInicio ? 1 : 0;
        var offsetFin = fin < inicio ? offsetInicio + 1 : offsetInicio;
        return SubFranja.Crear(inicio, fin, offsetInicio, offsetFin);
    }

    // DTO plano propio de esta isla; el FA lo mapea a DetalleFranjaOrdinaria solo para los eventos que
    // cruzan el bus.
    public FranjaProgramada ToDetalle() => new(
        _horaInicio, _horaFin, _diaOffsetFin,
        _descansos.Select(d => d.ToDetalle()).ToList().AsReadOnly(),
        _extras.Select(e => e.ToDetalle()).ToList().AsReadOnly(),
        ToString(),
        _sede);

    // Notacion unica de las tools: "22:00-06:00+1, descanso 02:00+1-02:30+1, extra ...". La sede no
    // entra: la malla muestra la efectiva, que resuelve la cascada al programar.
    public override string ToString() => string.Join(", ",
    [
        $"{FormatearHora(_horaInicio, 0)}-{FormatearHora(_horaFin, _diaOffsetFin)}",
        .. _descansos.Select(d => $"{FranjaTemporal.Mensajes.LabelDescanso} {d}"),
        .. _extras.Select(e => $"{FranjaTemporal.Mensajes.LabelExtra} {e}")
    ]);

    // _sede entra en la igualdad: es identidad del diseno de la franja, a diferencia de Descripcion
    // (derivado, en FranjaProgramada).
    public bool Equals(FranjaOrdinaria? other)
    {
        if (other is null) return false;
        if (_horaInicio != other._horaInicio || _horaFin != other._horaFin
            || _diaOffsetFin != other._diaOffsetFin) return false;
        return _descansos.SequenceEqual(other._descansos)
            && _extras.SequenceEqual(other._extras)
            && _sede == other._sede;
    }

    public override bool Equals(object? obj) => Equals(obj as FranjaOrdinaria);

    public override int GetHashCode()
    {
        var hash = HashCode.Combine(_horaInicio, _horaFin, _diaOffsetFin);
        foreach (var d in _descansos) hash = HashCode.Combine(hash, d);
        foreach (var e in _extras) hash = HashCode.Combine(hash, e);
        return HashCode.Combine(hash, _sede);
    }

    private static void ValidarContencion(FranjaOrdinaria contenedor, List<FranjaTemporal> hijas)
    {
        if (hijas.Any(h => !h.EstaContenidaEn(contenedor)))
            throw new ArgumentException(FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor);
    }

    // Fin exclusivo: contiguas no se solapan.
    private static void ValidarSolapamiento(List<FranjaTemporal> hijas)
    {
        for (var i = 0; i < hijas.Count; i++)
            for (var j = i + 1; j < hijas.Count; j++)
                if (hijas[i].SeSolapaCon(hijas[j]))
                    throw new ArgumentException(FranjaTemporal.Mensajes.FranjasHijasSeSuperponen);
    }

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
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

            // Se omite del JSON cuando es null: retrocompatibilidad con eventos sin sede.
            var fSede = typeof(FranjaOrdinaria)
                .GetField("_sede", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var pSede = typeInfo.CreateJsonPropertyInfo(typeof(SedeProgramada), "sede");
            pSede.Get = obj => fSede.GetValue(obj);
            pSede.Set = (obj, val) => fSede.SetValue(obj, val);
            pSede.ShouldSerialize = (_, val) => val is not null;
            typeInfo.Properties.Add(pSede);
        });
    }
}
