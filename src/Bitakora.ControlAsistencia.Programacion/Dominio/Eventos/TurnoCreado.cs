using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.Dominio.Comandos;

namespace Bitakora.ControlAsistencia.Programacion.Dominio.Eventos;

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

    // CA-14: el evento nunca se construye en estado invalido
    // CA-10: acumula TODOS los errores antes de lanzar AggregateException
    // CA-11: cada error individual es una ArgumentException
    public static TurnoCreado Crear(CrearTurno comando)
    {
        var errores = new List<Exception>();

        // CA-7: validar nombre no vacio
        if (string.IsNullOrWhiteSpace(comando.Nombre))
            errores.Add(new ArgumentException(Mensajes.NombreVacio));

        // CA-6: validar al menos una franja ordinaria
        if (comando.Ordinarias.Count == 0)
            errores.Add(new ArgumentException(Mensajes.SinFranjasOrdinarias));
        else if (HaySolapamientoEntreOrdinarias(comando.Ordinarias))
            // CA-8: solapamiento entre ordinarias -- un unico error independiente de cuantos pares
            errores.Add(new ArgumentException(Mensajes.FranjasOrdinariasSeSolapan));

        // CA-9: construir VOs delegando validacion a FranjaOrdinaria.Crear() y acumulando errores
        var franjasOrdinarias = new List<FranjaOrdinaria>();
        foreach (var franja in comando.Ordinarias)
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

        return new TurnoCreado(comando.TurnoId, comando.Nombre, franjasOrdinarias);
    }

    // Detecta si algún par de franjas ordinarias se solapa usando minutos absolutos desde el dia base.
    // No puede usar FranjaTemporal.MinutosAbsoluto* (internal en Contracts) -- calcula directo desde el DTO.
    private static bool HaySolapamientoEntreOrdinarias(List<CrearTurno.Franja> franjas)
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
