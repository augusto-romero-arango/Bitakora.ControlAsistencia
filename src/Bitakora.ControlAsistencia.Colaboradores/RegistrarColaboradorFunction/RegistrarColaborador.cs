namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction;

// Issue #330: comando para registrar un colaborador bajo control de asistencia.
// Trigger: HTTP POST, Route: Colaboradores.
// Payload primitivo -- NUNCA reusa un tipo de Colaboradores.DomainEvents como campo (MEF-ADR-0039
// decision 6, payload por rol): el handler construye TipoIdentificacion/Identificacion/
// NombreColaborador a partir de estos primitivos (parseo tipado unico en el borde, MEF-ADR-0037
// seccion 2).
// FechaInicio es REQUERIDA (DateOnly, sin default del servidor) -- doctrina bitemporal del BC: el
// tiempo de los hechos viene del cliente, nunca del reloj del servidor (la migracion trae fechas
// pasadas).
// Issue #520: CodigoSede OPCIONAL -- si ya se conoce la sede al ingreso, evita una segunda peticion
// a AsignarSede. null = sin sede (mismo default que antes de este issue).
public record RegistrarColaborador(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido,
    string CodigoColaborador,
    DateOnly FechaInicio,
    string? CodigoSede = null);
