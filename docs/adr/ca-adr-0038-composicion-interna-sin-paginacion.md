# CA-ADR-0038: Las llamadas internas de composicion no paginan

## Estado
Aceptado. Desviacion de MEF-ADR-0042 seccion 2 ("el `Take` del cliente se acota en el servidor"). Formato de desviacion como CA-ADR-0037.

## Contexto
Las tools del asistente que componen una operacion interna (programar un turno a una lista o grupo de colaboradores, aplicar plantillas semanales) necesitan todos los colaboradores que cumplen el criterio. `QUERY colaboradores/directorio` rechazaba mas de 200 identificaciones y recortaba a 200; `QUERY colaboradores/fichas` obligaba a paginar de 200 en 200, forzando ciclos.

## Decision
1. La paginacion y los topes pertenecen a la superficie que le muestra resultados a un consumidor final (tools que responden al modelo, un front). Las llamadas internas de composicion piden el resultado completo omitiendo `Take`. Cada llamador decide: si manda `Take`, pagina; si no, recibe todo.
2. `directorio`: `Take` es opcional. Con `Identificaciones` (con o sin `Nombre`) sin `Take` devuelve todas las coincidencias, y con `Take` lo respeta sin tope superior (minimo 1); desaparece el limite de 200 identificaciones. Solo por `Nombre` conserva default 50 y tope 200. No se desvia de MEF-ADR-0042: el resultado queda acotado por la entrada, y Marten traduce el filtro a `= ANY(:p)` con un unico parametro arreglo.
3. `fichas`: `Take` es opcional. Sin `Take` devuelve todas las fichas vigentes del filtro, sin tope. Con `Take` la paginacion keyset (tope 200) queda intacta. Aqui si hay desviacion: el resultado no queda acotado por la entrada.

## Consecuencias
- Riesgo aceptado: la respuesta de `fichas` sin `Take` crece con el tamano del grupo.
- Los llamadores existentes (`Mcp.Asistente`) ya mandan `Take` explicito y no cambian; #826 los mueve.
- Criterio de revision: cuando se aborde el tamano de las peticiones grandes (cuerpos con miles de identificaciones, miles de POST por lote), se reevalua esta decision.
