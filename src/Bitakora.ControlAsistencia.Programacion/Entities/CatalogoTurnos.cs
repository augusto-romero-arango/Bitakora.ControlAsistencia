using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

// HU-4: Aggregate root del catalogo de turnos de trabajo
// ADR-0015: partial class para soportar clase Mensajes en archivo separado
// Interfaz publica: Apply(TurnoCreado), Apply(TurnoRetirado), ToString()
// Estado interno (privado): nombre, franjas ordinarias, activo
public partial class CatalogoTurnos : AggregateRoot
{
    private string _nombre = string.Empty;
    private List<FranjaOrdinaria> _franjasOrdinarias = [];
    private bool _estaActivo;

    // CA-1: aplica TurnoCreado y establece estado interno del aggregate
    // Establece: Id (heredado de AggregateRoot), nombre, franjas ordinarias, activo=true
    public void Apply(TurnoCreado evento)
    {
        Id = evento.TurnoId.ToString();
        _nombre = evento.Nombre;
        _franjasOrdinarias = evento.FranjasOrdinarias.ToList();
        _estaActivo = true;
    }

    // Issue #500: primer consumidor real de _estaActivo -- apaga el turno, ya no asignable a
    // nuevas solicitudes. Nunca lanza (MEF-ADR-0004 capa 4): la guarda de "ya retirado" decide en
    // Retirar(), antes de emitir.
    public void Apply(TurnoRetirado evento) => throw new NotImplementedException();

    // Estado observable internal (Tell-don't-Ask, MEF-ADR-0012): existe para que el DSL de tests
    // lo verifique, no para consumo externo al ensamblado -- ninguna propiedad PUBLICA expone
    // _estaActivo.
    internal bool EstaActivo => _estaActivo;

    // Mecanismo "declinar con resultado" (CA-ADR-0030): el aggregate nunca lanza -- retorna la
    // razon del rechazo y el handler la traduce al status code (409 Conflict).
    internal ResultadoRetiroTurno Retirar() => throw new NotImplementedException();

    // La estructura cero-franjas ES el descanso: sin discriminador en el estado del aggregate.
    public override string ToString() => _franjasOrdinarias.Count == 0
        ? $"{_nombre} {Mensajes.LabelDescanso}"
        : $"{_nombre} {string.Join("", _franjasOrdinarias)}";

    // Devuelve el turno programado propio del dominio (Programacion.DomainEvents.TurnoProgramado).
    // Issue #319 (tres islas): ya no construye el DTO de bus (DetalleTurno, PrivateEvents) -- el
    // FA mapea TurnoProgramado -> DetalleTurno solo para los eventos que cruzan el bus (CA-5).
    internal TurnoProgramado ObtenerDetalle() => new(
        _nombre,
        _franjasOrdinarias.Select(f => f.ToDetalle()).ToList().AsReadOnly(),
        ToString());

    // Factory interno: crea el aggregate con el evento en _uncommittedEvents
    // Usado por el handler para StartStream -- no es parte de la interfaz publica del dominio
    internal static CatalogoTurnos Iniciar(TurnoCreado evento)
    {
        var catalogo = new CatalogoTurnos();
        catalogo._uncommittedEvents.Add(evento);
        catalogo.Apply(evento);
        return catalogo;
    }
}
