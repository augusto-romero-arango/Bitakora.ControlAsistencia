using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Sedes.Entities;

// Issue #456: primer aggregate del dominio Sedes. Identidad natural: el CodigoSede que trae el
// cliente (debe casar con los ids opacos que ya circulan en SedeProgramada.Id, #331).
//
// Anatomia CA-ADR-0031 (registro pendiente en el ADR, seccion 4): "s:{codigo}" -- prefijo "s"
// (iniciales del aggregate, paso 2 de la heuristica), separador ":" (paso 4, fuera del charset
// URL-safe del componente que ValidacionesCompartidasSedes ya garantiza en el borde). Un solo
// componente de dominio (Codigo): el split siempre devuelve exactamente 2 partes (paso 5).
//
// Estado observable: internal (Tell-don't-Ask, MEF-ADR-0012) -- el issue no expone Nombre/Ciudad/
// Direccion al exterior del ensamblado; existen para que el DSL de tests (And<>) verifique el
// estado rehidratado, mismo patron que ColaboradorAggregateRoot.
public partial class SedeAggregateRoot : AggregateRoot
{
    private const string PrefijoStreamId = "s";
    private const char SeparadorStreamId = ':';

    private string? _nombre;
    private string? _ciudad;
    private string? _direccion;

    internal string Nombre => _nombre!;
    internal string? Ciudad => _ciudad;
    internal string? Direccion => _direccion;

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

    // Factory interno: el handler decide si el stream ya existe (patron RegistrarColaborador,
    // CA-ADR-0030) antes de invocar este factory -- el aggregate no interroga su propio estado.
    internal static SedeAggregateRoot Registrar(
        string codigo, string nombre, string? ciudad, string? direccion)
    {
        var sede = new SedeAggregateRoot();

        var evento = new SedeRegistrada(codigo, nombre, ciudad, direccion);
        sede._uncommittedEvents.Add(evento);
        sede.Apply(evento);

        return sede;
    }

    // Issue #457 (MEF-ADR-0043 paso 2): reemplazo completo del nombre -- VO atomico direccionable
    // por {codigo}. El handler ya resolvio que el stream existe (CA-ADR-0030): el aggregate no
    // vuelve a interrogar su propio estado. Fase roja: stub minimo, el implementer completa.
    public void Apply(NombreSedeModificado e) => throw new NotImplementedException();

    internal void ModificarNombre(string nombre) => throw new NotImplementedException();

    // Issue #457 (MEF-ADR-0043 paso 2): reemplazo completo de Ciudad+Direccion como valor atomico.
    // La bandera Activa (issue #459, sin implementar todavia) no gobierna esta operacion -- una
    // sede desactivada sigue siendo editable (CA-5).
    public void Apply(UbicacionActualizada e) => throw new NotImplementedException();

    internal void ActualizarUbicacion(string? ciudad, string? direccion) =>
        throw new NotImplementedException();
}
