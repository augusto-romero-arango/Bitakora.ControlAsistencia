using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

// Issue #3: evento que registra la creacion de un turno de trabajo
// ADR-0015: sealed class porque contiene IReadOnlyList<FranjaOrdinaria> -- record no puede
//           garantizar igualdad por valor en colecciones mutables
// CA-12: factory static Crear(), constructor privado
// CA-13: constructor vacio privado solo para Marten/JSON
public sealed partial class TurnoCreado
{
    public Guid TurnoId { get; private set; }
    public string Nombre { get; private set; }
    public IReadOnlyList<FranjaOrdinaria> FranjasOrdinarias { get; private set; }

    // CA-12: constructor real privado -- solo el factory lo invoca
    private TurnoCreado(Guid turnoId, string nombre, IReadOnlyList<FranjaOrdinaria> franjasOrdinarias)
    {
        TurnoId = turnoId;
        Nombre = nombre;
        FranjasOrdinarias = franjasOrdinarias;
    }

    // CA-13: constructor vacio privado para Marten/JSON
    private TurnoCreado()
    {
        Nombre = string.Empty;
        FranjasOrdinarias = [];
    }

    // Mapping de serializacion para STJ/Marten - mismo patron que FranjaOrdinaria
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

    // CA-14: el evento nunca se construye en estado invalido
    // CA-10: acumula TODOS los errores antes de lanzar AggregateException
    // CA-11: cada error individual es una ArgumentException
    public static TurnoCreado Crear(Guid turnoId, string nombre, IReadOnlyList<DatosFranja> ordinarias)
    {
        var errores = new List<Exception>();

        // CA-7: validar nombre no vacio
        if (string.IsNullOrWhiteSpace(nombre))
            errores.Add(new ArgumentException(Mensajes.NombreVacio));

        // CA-6: validar al menos una franja ordinaria
        if (ordinarias.Count == 0)
            errores.Add(new ArgumentException(Mensajes.SinFranjasOrdinarias));
        else if (HaySolapamientoEntreOrdinarias(ordinarias))
            // CA-8: solapamiento entre ordinarias -- un unico error independiente de cuantos pares
            errores.Add(new ArgumentException(Mensajes.FranjasOrdinariasSeSolapan));

        // CA-9: construir VOs delegando validacion a FranjaOrdinaria.Crear() y acumulando errores
        var franjasOrdinarias = new List<FranjaOrdinaria>();
        foreach (var franja in ordinarias)
        {
            try
            {
                var descansos = franja.Descansos.Select(d => SubFranja.Crear(d.inicio, d.fin));
                var extras = franja.Extras.Select(e => SubFranja.Crear(e.inicio, e.fin));
                franjasOrdinarias.Add(FranjaOrdinaria.Crear(franja.Inicio, franja.Fin,
                    descansos: descansos, extras: extras));
            }
            catch (ArgumentException ex)
            {
                errores.Add(ex);
            }
        }

        if (errores.Count > 0)
            throw new AggregateException(errores);

        return new TurnoCreado(turnoId, nombre, franjasOrdinarias);
    }

    // Detecta si algun par de franjas ordinarias se solapa usando minutos absolutos desde el dia base.
    // Duplicacion deliberada respecto de FranjaTemporal.SeSolapaCon -- decidida en #272 y #285,
    // no es deuda pendiente. Dos razones:
    // 1. Son reglas distintas con divergencia plausible (MEF-ADR-0018): aqui se validan ordinarias
    //    entre si sobre DatosFranja crudos; alla, hijas contra su contenedor sobre VOs construidos.
    // 2. El orden del factory lo exige: este chequeo corre ANTES de construir las FranjaOrdinaria
    //    para acumular todos los errores sin fail-fast (CA-10 de #3).
    private static bool HaySolapamientoEntreOrdinarias(IReadOnlyList<DatosFranja> franjas)
    {
        const int minsPorHora = 60;
        const int minsPorDia = 1440;

        var absolutas = franjas.Select(f =>
        {
            var offsetFin = f.Fin < f.Inicio ? 1 : 0;
            var inicio = f.Inicio.Hour * minsPorHora + f.Inicio.Minute;
            var fin = f.Fin.Hour * minsPorHora + f.Fin.Minute + offsetFin * minsPorDia;
            return (inicio, fin);
        }).ToList();

        return absolutas
            .SelectMany((a, i) => absolutas.Skip(i + 1).Select(b => (a, b)))
            .Any(par => par.a.inicio < par.b.fin && par.b.inicio < par.a.fin);
    }
}
