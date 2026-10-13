# CA-ADR-0041: Smoke tests sin validaciones de tamano

## Estado
Aceptado (2026-10-12). Precisa MEF-ADR-0013 (smoke tests contra dev). Origen: issue #955. Regla provisional mientras Mefisto la incorpora al ADR del marco y al `smoke-test-writer` (augusto-romero-arango/mefisto#2366).

## Contexto
`ListarDirectorioColaboradoresSmokeTests.ListarDirectorioColaboradores_Retorna422_CuandoIdentificacionesSuperaDoscientosValores` esperaba 422 con 201 identificaciones y recibia 200. El 200 era correcto: #830 (CA-ADR-0038) elimino a proposito ese tope. El smoke quedo obsoleto porque validaba un tope. Esa clase de validacion es logica pura del endpoint o de la tool; en smoke carga la base de dev y el tiempo del job de CI en cada ejecucion, y deja de ser cierta cuando la regla cambia.

## Decision
1. Los smoke tests **no validan** tamanos de pagina, clamp o topes de `Take`, maximos de valores de un filtro ni topes de ventana de fechas, y **no siembran volumenes** para ejercerlos.
2. Esas reglas se cubren con tests unitarios o de endpoint del dominio (o de la tool).
3. En smoke siguen: una llamada real por endpoint o tool, el recorrido con cursor de una pagina a otra y la validacion de entrada barata (cursor invalido -> 422/400).
4. Un smoke existente que viole (1) se elimina, no se adapta; su comportamiento debe quedar cubierto en la suite unitaria o de endpoint antes de borrarlo.

## Consecuencias
- Los smoke de listados quedan mas cortos y no dependen de volumen de datos en dev.
- Un cambio de tope o de tamano de pagina solo toca tests unitarios o de endpoint.
- Se pierde la verificacion contra dev de esos limites; es aceptable porque no dependen de infraestructura.
- ControlHoras, Programacion y el MCP aplican la misma regla en sus suites (#972, #973, #974).
