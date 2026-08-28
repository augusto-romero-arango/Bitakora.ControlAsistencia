namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;

// Issue #457 (MEF-ADR-0043 paso 2): comando interno para reemplazar completo el nombre de una sede
// existente -- VO atomico direccionable por {codigo}. Trigger HTTP PUT,
// Route: sedes/{codigo}/nombre. Precedente: CorregirNombres (Colaboradores, issue #377) -- el
// endpoint compone este comando desde {codigo} de ruta + ModificarNombreSedeBody.
public record ModificarNombreSede(string Codigo, string Nombre);
