# CA-ADR-0040: Eliminacion de datos de smoke que pueden volver fragil al test

## Estado
Aceptado (2026-10-12). Precisa MEF-ADR-0013 (smoke tests contra dev). Origen: issue #952.

## Contexto
MEF-ADR-0013 aisla los datos de cada smoke con IDs unicos (`Guid.CreateVersion7()`) y no exige cleanup. Eso basta cuando la unicidad es por llave. Una Jornada, en cambio, se rechaza con 409 si sus limites (horas semanales, tope diario, minimo diario, dias de descanso) igualan los de otra del tenant, incluidas las desactivadas (#869). La restriccion es por contenido: dos jornadas con GUIDs distintos chocan igual. Los smoke de Programacion generaban limites con `Random` (59 combinaciones), las jornadas se acumulaban en dev y el run de CI fallaba con 409 en 9 de 14 casos.

## Decision
1. **Unicidad por llave**: el aislamiento por GUID de MEF-ADR-0013 es suficiente; no hay cleanup.
2. **Restriccion por contenido (o singleton compartido)**: el test usa una **firma fija propia**, tomada de un catalogo (`Fixtures/LimitesDePrueba.cs`), purga por contenido esa firma antes del Act y borra al final, aunque falle, lo que crea. El borrado se hace por conexion directa a Postgres acotada al tenant de smoke (`tenant-smoke`), sin endpoint de limpieza (mismo criterio que #901: no se agrega superficie destructiva al paquete de produccion). Sin Postgres configurado, el test se omite.
3. **Firmas reservadas**: las firmas compartidas entre suites (hoy 42 h / 8 h / 0 / 1 descanso, la de la jornada predeterminada que reutilizan los smoke de ControlHoras y del MCP) son reservadas: ninguna entrada del catalogo puede igualarlas y la purga nunca las toca. El catalogo valida en la inicializacion que sus firmas sean distintas entre si y de la reservada.
4. **Alcance vigente**: solo jornadas de `Programacion.SmokeTests`. Extender la politica a otro dato requiere su propio issue.
5. **Dependientes**: lo que referencia a la jornada (plantillas, cuadros, solicitudes) no se borra; tiene GUID propio y no genera fragilidad. Excepcion: si el test vuelve predeterminada una jornada que luego borra, restaura la predeterminada previa (o borra Preferencias para que el asegurador la recree al primer uso); una referencia colgante haria responder 404 a la programacion sin jornada explicita de cualquier suite.

## Consecuencias
- Los smoke de jornadas son deterministas: dos runs seguidos no colisionan y no queda residuo con firmas del catalogo.
- Cada test que crea jornadas depende de Postgres y se omite sin el.
- Un run abortado puede dejar residuo, que la purga previa por contenido recoge en el siguiente.
- Agregar un test con jornada exige una entrada nueva en el catalogo.
