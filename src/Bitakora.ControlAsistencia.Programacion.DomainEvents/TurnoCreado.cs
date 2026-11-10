using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// sealed class y no record: contiene IReadOnlyList<FranjaOrdinaria> y un record no garantiza igualdad
// por valor en colecciones.
public sealed partial class TurnoCreado
{
    public Guid TurnoId { get; private set; }
    public string Nombre { get; private set; }
    public IReadOnlyList<FranjaOrdinaria> FranjasOrdinarias { get; private set; }
    public bool EsDescanso { get; private set; }
    public bool EsHorarioAbierto { get; private set; }

    private TurnoCreado(
        Guid turnoId, string nombre, IReadOnlyList<FranjaOrdinaria> franjasOrdinarias, bool esDescanso)
    {
        TurnoId = turnoId;
        Nombre = nombre;
        FranjasOrdinarias = franjasOrdinarias;
        EsDescanso = esDescanso;
    }

    // Constructor vacio privado para Marten/JSON.
    private TurnoCreado()
    {
        Nombre = string.Empty;
        FranjasOrdinarias = [];
    }

    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver)
    {
        var ctor = typeof(TurnoCreado)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes)!;

        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Type != typeof(TurnoCreado)) return;
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            typeInfo.CreateObject = () => (TurnoCreado)ctor.Invoke(null);

            // Propiedades con private set: registrar setters via backing fields
            foreach (var prop in typeInfo.Properties)
            {
                if (prop.Set is not null) continue;
                var backingField = typeof(TurnoCreado).GetField(
                    $"<{prop.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (backingField is not null)
                    prop.Set = (obj, val) => backingField.SetValue(obj, val);
            }
        });
    }

    // Acumula TODOS los errores (cada uno ArgumentException) antes de lanzar AggregateException.
    public static TurnoCreado Crear(Guid turnoId, string nombre, IReadOnlyList<DatosFranja> ordinarias)
    {
        var errores = new List<Exception>();

        if (string.IsNullOrWhiteSpace(nombre))
            errores.Add(new ArgumentException(Mensajes.NombreVacio));

        // Un turno nace vacio y se completa por pasos (CA-ADR-0033): cero franjas es valido.
        if (HaySolapamientoEntreOrdinarias(ordinarias))
            // Un unico error, sin importar cuantos pares se solapen.
            errores.Add(new ArgumentException(Mensajes.FranjasOrdinariasSeSolapan));

        var franjasOrdinarias = new List<FranjaOrdinaria>();
        foreach (var franja in ordinarias)
        {
            try
            {
                var ordinaria = FranjaOrdinaria.Crear(
                    franja.Inicio, franja.Fin, franja.DiaOffsetFin, sede: franja.Sede);
                ordinaria = franja.Descansos.Aggregate(
                    ordinaria, (acc, d) => acc.ConDescanso(d.inicio, d.fin));
                ordinaria = franja.Extras.Aggregate(
                    ordinaria, (acc, e) => acc.ConExtra(e.inicio, e.fin));
                franjasOrdinarias.Add(ordinaria);
            }
            catch (ArgumentException ex)
            {
                errores.Add(ex);
            }
        }

        if (errores.Count > 0)
            throw new AggregateException(errores);

        return new TurnoCreado(turnoId, nombre, franjasOrdinarias, esDescanso: false);
    }

    // La marca EsDescanso es la unica frontera entre un descanso y un turno incompleto: ambos
    // nacen con cero franjas ordinarias (CA-ADR-0033).
    public static TurnoCreado CrearDescanso(Guid turnoId, string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new AggregateException(new ArgumentException(Mensajes.NombreVacio));

        return new TurnoCreado(turnoId, nombre, [], esDescanso: true);
    }

    public static TurnoCreado CrearDeHorarioAbierto(
        Guid turnoId, string nombre, IReadOnlyList<DatosFranja> ordinarias)
    {
        var evento = Crear(turnoId, nombre, ordinarias);
        evento.EsHorarioAbierto = true;
        return evento;
    }

    // Duplicacion deliberada de FranjaTemporal.SeSolapaCon, no deuda: son reglas distintas con
    // divergencia plausible (ordinarias entre si sobre DatosFranja crudos; alla, hijas contra su
    // contenedor sobre VOs), y este chequeo corre ANTES de construir las FranjaOrdinaria para acumular
    // todos los errores sin fail-fast.
    private static bool HaySolapamientoEntreOrdinarias(IReadOnlyList<DatosFranja> franjas)
    {
        const int minsPorHora = 60;
        const int minsPorDia = 1440;

        var absolutas = franjas.Select(f =>
        {
            var offsetFin = f.DiaOffsetFin != 0 ? f.DiaOffsetFin : (f.Fin < f.Inicio ? 1 : 0);
            var inicio = f.Inicio.Hour * minsPorHora + f.Inicio.Minute;
            var fin = f.Fin.Hour * minsPorHora + f.Fin.Minute + offsetFin * minsPorDia;
            return (inicio, fin);
        }).ToList();

        return absolutas
            .SelectMany((a, i) => absolutas.Skip(i + 1).Select(b => (a, b)))
            .Any(par => par.a.inicio < par.b.fin && par.b.inicio < par.a.fin);
    }
}
