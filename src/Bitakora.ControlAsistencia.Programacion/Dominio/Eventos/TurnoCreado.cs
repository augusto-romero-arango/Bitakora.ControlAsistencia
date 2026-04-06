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
    public Guid TurnoId { get; }
    public string Nombre { get; }
    public IReadOnlyList<FranjaOrdinaria> FranjasOrdinarias { get; }

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
        => throw new NotImplementedException();
}
