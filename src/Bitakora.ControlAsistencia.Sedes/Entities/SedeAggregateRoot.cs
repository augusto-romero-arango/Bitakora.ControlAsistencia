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
    private bool _activa;

    internal string Nombre => _nombre!;
    internal string? Ciudad => _ciudad;
    internal string? Direccion => _direccion;
    internal string? CentroDeCostos => _centroDeCostos;
    internal bool Activa => _activa;

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
        // La sede nace activa: no hay evento inicial de activacion que aplicar.
        _activa = true;
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

    public void Apply(SedeActivada e) => _activa = true;

    internal ResultadoActivacionSede Activar()
    {
        if (_activa)
            return ResultadoActivacionSede.YaActiva;

        var evento = new SedeActivada();
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoActivacionSede.Exitosa;
    }

    public void Apply(SedeDesactivada e) => _activa = false;

    internal ResultadoDesactivacionSede Desactivar()
    {
        if (!_activa)
            return ResultadoDesactivacionSede.YaInactiva;

        var evento = new SedeDesactivada();
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoDesactivacionSede.Exitosa;
    }

    // Issue #460: coleccion interna de dispositivos instalados -- Tell-don't-Ask (MEF-ADR-0012),
    // el dispositivo es ajeno al sistema (DispositivoId opaco), no es aggregate root propio.
    private readonly List<string> _dispositivosInstalados = [];

    internal IReadOnlyCollection<string> DispositivosInstalados => _dispositivosInstalados;

    public void Apply(DispositivoInstalado e) => _dispositivosInstalados.Add(e.DispositivoId);

    internal ResultadoInstalacionDispositivo InstalarDispositivo(string dispositivoId)
    {
        if (_dispositivosInstalados.Contains(dispositivoId))
            return ResultadoInstalacionDispositivo.YaInstalado;

        var evento = new DispositivoInstalado(dispositivoId);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoInstalacionDispositivo.Exitosa;
    }

    public void Apply(DispositivoRetirado e) => _dispositivosInstalados.Remove(e.DispositivoId);

    internal ResultadoRetiroDispositivo RetirarDispositivo(string dispositivoId)
    {
        if (!_dispositivosInstalados.Contains(dispositivoId))
            return ResultadoRetiroDispositivo.NoInstalado;

        var evento = new DispositivoRetirado(dispositivoId);
        _uncommittedEvents.Add(evento);
        Apply(evento);

        return ResultadoRetiroDispositivo.Exitosa;
    }
}
