namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

public abstract class PrecondicionComandoException(string message) : Exception(message);

public sealed class RecursoYaExisteException(string message) : PrecondicionComandoException(message);

public sealed class RecursoNoEncontradoException(string message) : PrecondicionComandoException(message);

public sealed class ReglaDeNegocioDeclinadaException(string message) : PrecondicionComandoException(message);
