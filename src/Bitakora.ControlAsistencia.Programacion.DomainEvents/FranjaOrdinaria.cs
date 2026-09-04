using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Segmento continuo de trabajo dentro de un turno.
// Contiene sub-franjas de descanso y extras.
// ADR-0015: sealed class con factory static, constructor privado, campos readonly.
public sealed partial class FranjaOrdinaria : FranjaTemporal, IEquatable<FranjaOrdinaria>
{
    private readonly List<SubFranja> _descansos;
    private readonly List<SubFranja> _extras;

    // Issue #335: sede prearmada para esta franja del catalogo (null = sin sede asignada). Campo
    // interno, sin propiedad publica (MEF-ADR-0012, Tell-don't-Ask) -- se expone SOLO via
    // ToDetalle() y ToString().
    private readonly SedeProgramada? _sede;

    // Constructor real: usado por el factory
    // diaOffsetInicio siempre es 0 — la ordinaria empieza en el dia base
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

    // CA-8: factory estatico
    // Issue #598 CA-4 a CA-6: rechaza duracion no positiva (generaliza inicio == fin) y duracion
    // mayor a 24 horas (jornada maxima; 24 h exactas se acepta para esquemas 24x24)
    // CA-13: infiere offset +1 cuando fin < inicio
    // CA-14 a CA-16: valida que descansos y extras esten contenidos
    // CA-17 a CA-19: valida que descansos y extras no se solapen entre si
    // Issue #335 CA-3: la sede prearmada, si viene, debe tener Id y Nombre no vacios/en blanco --
    // se valida junto a las demas invariantes de la franja (no en el RequestValidator del comando).
    public static FranjaOrdinaria Crear(
        TimeOnly horaInicio,
        TimeOnly horaFin,
        int diaOffsetFin = 0,
        IEnumerable<SubFranja>? descansos = null,
        IEnumerable<SubFranja>? extras = null,
        SedeProgramada? sede = null)
    {
        // CA-13: inferir offset cuando fin < inicio y no se especifico
        if (diaOffsetFin == 0 && horaFin < horaInicio)
            diaOffsetFin = 1;

        // CA-3: rechazar sede incompleta -- la regla de completitud la responde la propia sede
        if (sede is not null && !sede.EstaCompleta())
            throw new ArgumentException(Mensajes.SedeIncompleta);

        var listaDescansos = descansos?.ToList() ?? [];
        var listaExtras = extras?.ToList() ?? [];

        var ordinaria = new FranjaOrdinaria(horaInicio, horaFin, diaOffsetFin,
            listaDescansos, listaExtras, sede);

        // Issue #598: la propia franja responde su duracion (Tell-don't-Ask, MEF-ADR-0012)
        var duracionEnMinutos = ordinaria.DuracionEnMinutos();
        if (duracionEnMinutos <= 0)
            throw new ArgumentException(FranjaTemporal.Mensajes.DuracionNoPositiva);
        if (duracionEnMinutos > MinutosPorDia)
            throw new ArgumentException(Mensajes.DuracionExcedeUnDia);

        // Proyectar todas las hijas como FranjaTemporal para validaciones unificadas
        var hijas = listaDescansos.Cast<FranjaTemporal>().Concat(listaExtras).ToList();

        // CA-14 a CA-16: validar contencion de todas las hijas
        ValidarContencion(ordinaria, hijas);

        // CA-17 a CA-19: validar que no haya solapamiento entre hijas
        ValidarSolapamiento(hijas);

        return ordinaria;
    }

    // Issue #600: agrega una hija infiriendo sus offsets relativos a esta franja (ver ConHija) y
    // devuelve una nueva instancia -- inmutabilidad de VO, revalidada integramente via Crear.
    public FranjaOrdinaria ConDescanso(TimeOnly inicio, TimeOnly fin) =>
        Crear(_horaInicio, _horaFin, _diaOffsetFin, [.. _descansos, ConHija(inicio, fin)], _extras, _sede);

    public FranjaOrdinaria ConExtra(TimeOnly inicio, TimeOnly fin) =>
        Crear(_horaInicio, _horaFin, _diaOffsetFin, _descansos, [.. _extras, ConHija(inicio, fin)], _sede);

    // Issue #600: unico punto de inferencia de offsets de una hija, relativa al inicio de esta
    // franja (Tell-don't-Ask, MEF-ADR-0012). Con la ordinaria acotada a <= 24h (#598), el dia de
    // "inicio" es unico (anterior a medianoche si es previo al inicio de la franja) y el de "fin"
    // es el primer instante posterior con esa hora.
    private SubFranja ConHija(TimeOnly inicio, TimeOnly fin)
    {
        var offsetInicio = inicio < _horaInicio ? 1 : 0;
        var offsetFin = fin < inicio ? offsetInicio + 1 : offsetInicio;
        return SubFranja.Crear(inicio, fin, offsetInicio, offsetFin);
    }

    // Conversion al DTO plano propio del dominio (Programacion.DomainEvents.FranjaProgramada).
    // Issue #319 (tres islas): ya no retorna el DTO de bus (DetalleFranjaOrdinaria, PrivateEvents)
    // -- el FA mapea FranjaProgramada -> DetalleFranjaOrdinaria solo para los eventos que cruzan
    // el bus (CA-5). Tell-don't-Ask preservado: la conversion sigue viviendo en este VO, sin abrir
    // _descansos/_extras (MEF-ADR-0012).
    // Issue #335 CA-1/CA-2: copia la sede prearmada al DTO plano (o null si no hay).
    public FranjaProgramada ToDetalle() => new(
        _horaInicio, _horaFin, _diaOffsetFin,
        _descansos.Select(d => d.ToDetalle()).ToList().AsReadOnly(),
        _extras.Select(e => e.ToDetalle()).ToList().AsReadOnly(),
        ToString(),
        _sede);

    // CA-20, CA-21: formato "(06:00-12:00)" o "(22:00-06:00+1)"
    // Issue #335: incluye el label de sede (.resx) cuando la franja la trae prearmada.
    public override string ToString()
    {
        var resultado = $"({FormatearHora(_horaInicio, 0)}-{FormatearHora(_horaFin, _diaOffsetFin)})";

        if (_descansos.Count > 0)
            resultado += $"[{FranjaTemporal.Mensajes.LabelDescansos}:{string.Join(", ", _descansos)}]";

        if (_extras.Count > 0)
            resultado += $"[{FranjaTemporal.Mensajes.LabelExtras}:{string.Join(", ", _extras)}]";

        if (_sede is not null)
            resultado += $"[{Mensajes.LabelSede}:{_sede.Nombre}]";

        return resultado;
    }

    // Igualdad por valor
    // Issue #335 CA-5: _sede entra en Equals/GetHashCode -- es dato de identidad del diseno de la
    // franja (que sede prearmo el catalogo), a diferencia de Descripcion (derivado, en FranjaProgramada).
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

    // Verifica que cada franja hija este contenida dentro de la ordinaria
    private static void ValidarContencion(FranjaOrdinaria contenedor, List<FranjaTemporal> hijas)
    {
        if (hijas.Any(h => !h.EstaContenidaEn(contenedor)))
            throw new ArgumentException(FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor);
    }

    // Verifica que ningun par de franjas hijas se solapen
    // CA-18: fin exclusivo, contiguas no se solapan
    private static void ValidarSolapamiento(List<FranjaTemporal> hijas)
    {
        for (var i = 0; i < hijas.Count; i++)
            for (var j = i + 1; j < hijas.Count; j++)
                if (hijas[i].SeSolapaCon(hijas[j]))
                    throw new ArgumentException(FranjaTemporal.Mensajes.FranjasHijasSeSuperponen);
    }

    // Mapping de serializacion - vive aqui porque cambia con la clase
    // Issue #335 CA-4/CA-5: _sede se registra como campo opcional -- STJ omite la clave "sede"
    // del JSON cuando el valor es null (retrocompatibilidad, CA-4), y la restaura en el round-trip
    // cuando esta presente (CA-5).
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

            // Sede prearmada: campo opcional -- se omite del JSON cuando es null (CA-4).
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
