using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.Colaboradores.Entities;

// Issue #330: aggregate root que representa a un colaborador bajo control de asistencia. Nace con
// este issue -- primer aggregate y primeros dos eventos persistidos del dominio Colaboradores.
// Identidad: Identificacion.ToString() ("CC:79543210"), no un Guid -- MEF-ADR-0037: punto unico de
// conversion de la identidad de stream via ComputarStreamId.
// Interfaz publica propuesta por el planner (issue #330): ComputarStreamId, Registrar (factory),
// Apply (publicos, exigidos por el TestStore via reflection). El estado interno de la vinculacion
// (codigo/fecha) NO es publico -- el issue no expone lectura; #349+ decidiran que observables
// necesitan las invariantes de no-solape/maximo una vigente. Se expone como internal
// (InternalsVisibleTo hacia Colaboradores.Tests, ver el .csproj) para que el harness de tests pueda
// verificar el estado sin ampliar la superficie publica de cara a otros consumidores del ensamblado.
// ADR-0015 (partial class): soporta clase Mensajes en archivo separado si se requiere (este aggregate
// no tiene eventos de fallo propios en este corte -- "Identificacion ya registrada" es precondicion
// de orquestacion del handler, MEF-ADR-0004 capa 2, no una regla de negocio del aggregate).
public partial class ColaboradorAggregateRoot : AggregateRoot
{
    private Identificacion? _identificacion;
    private NombreColaborador? _nombre;
    private string? _codigoVinculacionVigente;
    private DateOnly _fechaInicioVinculacionVigente;

    internal Identificacion Identificacion => _identificacion!;
    internal NombreColaborador Nombre => _nombre!;
    internal string CodigoVinculacionVigente => _codigoVinculacionVigente!;
    internal DateOnly FechaInicioVinculacionVigente => _fechaInicioVinculacionVigente;

    // Contrato: clave del stream de Colaborador. Delega en el ToString() canonico del VO (#348) --
    // ningun handler/endpoint concatena la clave por su cuenta (MEF-ADR-0037).
    public static string ComputarStreamId(Identificacion identificacion) => identificacion.ToString();

    // Apply: publicos -- requerido para que TestStore.ApplyEvent los encuentre via GetMethods().
    // Nunca lanzan (MEF-ADR-0004 capa 4) -- STUB (fase roja, issue #330).
    public void Apply(ColaboradorRegistrado e) => throw new NotImplementedException();

    public void Apply(VinculacionIniciada e) => throw new NotImplementedException();

    // Factory interno: agrega los DOS eventos del commit a _uncommittedEvents y los aplica -- patron
    // RegistroDeMarcacionAggregateRoot.Iniciar, generalizado a dos eventos en el mismo commit.
    // STUB (fase roja, issue #330): el implementer construye ColaboradorRegistrado(identificacion,
    // nombre) + VinculacionIniciada(codigo, fechaInicio), los agrega en ese orden y llama Apply.
    internal static ColaboradorAggregateRoot Registrar(
        Identificacion identificacion, NombreColaborador nombre, string codigo, DateOnly fechaInicio) =>
        throw new NotImplementedException();
}
