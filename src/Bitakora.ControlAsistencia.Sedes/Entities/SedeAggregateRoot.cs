using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Identidad natural: el CodigoSede que trae el cliente (debe casar con los ids opacos que ya
// circulan en SedeProgramada.Id).
//
// Anatomia CA-ADR-0031: "s:{codigo}" -- prefijo "s" (iniciales del aggregate), separador ":" (fuera
// del charset URL-safe que el borde garantiza para el codigo). Un solo componente de dominio: el
// split siempre devuelve exactamente 2 partes.
//
// Estado observable internal (Tell-don't-Ask, MEF-ADR-0012): existe para que el DSL de tests lo
// verifique, no para consumo externo al ensamblado.
public partial class SedeAggregateRoot : AggregateRoot
{
    private const string PrefijoStreamId = "s";
    private const char SeparadorStreamId = ':';

    private string? _nombre;
    private string? _ciudad;
    private string? _direccion;
    private string? _centroDeCostos;

    internal string Nombre => _nombre!;
    internal string? Ciudad => _ciudad;
    internal string? Direccion => _direccion;
    internal string? CentroDeCostos => _centroDeCostos;

    // Punto unico de conversion de la clave del stream (MEF-ADR-0037): ningun handler/endpoint la
    // concatena por su cuenta.
    public static string ComputarStreamId(string codigo) =>
        $"{PrefijoStreamId}{SeparadorStreamId}{codigo}";

    // Apply: publico -- requerido para que TestStore.ApplyEvent lo encuentre via GetMethods().
    // Nunca lanza (MEF-ADR-0004 capa 4).
    public void Apply(SedeRegistrada e)
    {
        Id = ComputarStreamId(e.Codigo);
        _nombre = e.Nombre;
        _ciudad = e.Ciudad;
        _direccion = e.Direccion;
    }

    // Los metodos de mutacion no interrogan el estado propio: el handler ya resolvio que el stream
    // existe (CA-ADR-0030), y una sede desactivada sigue siendo editable -- la bandera Activa no
    // gobierna estas operaciones.
    internal static SedeAggregateRoot Registrar(
        string codigo, string nombre, string? ciudad, string? direccion)
    {
        var sede = new SedeAggregateRoot();

        var evento = new SedeRegistrada(codigo, nombre, ciudad, direccion);
        sede._uncommittedEvents.Add(evento);
        sede.Apply(evento);

        return sede;
    }

    public void Apply(NombreSedeModificado e) => _nombre = e.Nombre;

    internal void ModificarNombre(string nombre)
    {
        var evento = new NombreSedeModificado(nombre);
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    // Reemplazo completo de Ciudad+Direccion: los nulos que trae el evento se escriben, no se
    // ignoran.
    public void Apply(UbicacionActualizada e)
    {
        _ciudad = e.Ciudad;
        _direccion = e.Direccion;
    }

    internal void ActualizarUbicacion(string? ciudad, string? direccion)
    {
        var evento = new UbicacionActualizada(ciudad, direccion);
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    public void Apply(CentroDeCostosAsignado e) => _centroDeCostos = e.CentroDeCostos;

    internal void AsignarCentroDeCostos(string centroDeCostos)
    {
        var evento = new CentroDeCostosAsignado(centroDeCostos);
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }

    public void Apply(CentroDeCostosRetirado e) => _centroDeCostos = null;

    internal ResultadoRetiroCentroDeCostos RetirarCentroDeCostos()
    {
        if (_centroDeCostos is null)
            return ResultadoRetiroCentroDeCostos.SinCentroDeCostosVigente;

        var evento = new CentroDeCostosRetirado();
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoRetiroCentroDeCostos.Exitosa;
    }
}
