# ADR-0003: Proyectos de tests separados por dominio

## Estado

Aceptado

## Contexto

Con multiples dominios en la solucion, un unico proyecto de tests unificado ejecutaria la
suite completa cada vez que se realiza un cambio en cualquier dominio. Para un equipo que
trabaja en marcaciones, ejecutar los tests de liquidacion y notificaciones en cada ciclo
de feedback es ruido innecesario que enlentece el desarrollo.

En CI, el costo es aun mayor: cada pull request que toca un solo dominio lanzaria la
ejecucion de cientos de tests que no tienen relacion con el cambio.

Ademas, un proyecto de tests unificado tiende a acumular dependencias de todos los dominios,
haciendo que los tests de un dominio puedan quebrarse por cambios en otro, creando
acoplamiento implicito.

## Decision

Se crea un proyecto xUnit por dominio (mas uno para el proyecto Contracts). Cada proyecto
de tests referencia unicamente el proyecto de dominio que le corresponde.

Estructura de proyectos resultante:

```
tests/
  Bitakora.ControlAsistencia.Contracts.Tests/
  Bitakora.ControlAsistencia.Marcaciones.Tests/
  Bitakora.ControlAsistencia.Empleados.Tests/
  Bitakora.ControlAsistencia.Liquidacion.Tests/
  Bitakora.ControlAsistencia.Notificaciones.Tests/
```

En CI, cada pipeline de dominio ejecuta solo su suite correspondiente:

```bash
dotnet test tests/Bitakora.ControlAsistencia.Marcaciones.Tests/
```

Para verificar la solucion completa (por ejemplo, antes de un merge a main) se puede
ejecutar `dotnet test` desde la raiz de la solucion.

## Consecuencias

**Positivas**

- Feedback rapido en CI: un cambio en marcaciones solo ejecuta los tests de marcaciones,
  no los de toda la solucion.
- Cada dominio tiene velocidad de tests independiente: un dominio con muchos tests de
  integracion no ralentiza los ciclos de otro dominio.
- Aislamiento de dependencias: los tests de un dominio no pueden acumular dependencias
  transitivas de otros dominios.

**Negativas**

- La solucion tiene mas proyectos, lo que incrementa el tiempo de carga inicial en el IDE
  y la cantidad de archivos `.csproj` a mantener.
