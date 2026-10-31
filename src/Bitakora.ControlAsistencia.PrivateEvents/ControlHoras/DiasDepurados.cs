using Cosmos.EventDriven.Abstractions;

namespace Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

public record DiasDepurados(
    string CodigoColaborador,
    IReadOnlyList<DiaDepurado> Dias) : IPrivateEvent;
