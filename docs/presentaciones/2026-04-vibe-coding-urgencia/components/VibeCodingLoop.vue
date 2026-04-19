<template>
  <svg
    viewBox="0 0 1000 720"
    class="mx-auto max-h-[72vh] w-auto"
    xmlns="http://www.w3.org/2000/svg"
  >
    <defs>
      <marker
        id="vcl-arrow"
        viewBox="0 0 10 10"
        refX="9"
        refY="5"
        markerWidth="6"
        markerHeight="6"
        orient="auto-start-reverse"
      >
        <path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor" />
      </marker>
      <marker
        id="vcl-arrow-thin"
        viewBox="0 0 10 10"
        refX="9"
        refY="5"
        markerWidth="5"
        markerHeight="5"
        orient="auto-start-reverse"
      >
        <path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor" opacity="0.75" />
      </marker>
    </defs>

    <!-- OUTER LOOP arcs: 1 -> 2 -> 3 -> 4 -> 5 -> 6 -> 1 -->
    <g
      stroke="currentColor"
      stroke-width="2"
      fill="none"
    >
      <path d="M 149 378 A 260 260 0 0 1 256 147" marker-end="url(#vcl-arrow)" />
      <path d="M 286 126 A 260 260 0 0 1 514 126" marker-end="url(#vcl-arrow)" />
      <path d="M 544 147 A 260 260 0 0 1 651 378" marker-end="url(#vcl-arrow)" />
      <path d="M 651 342 A 260 260 0 0 1 544 573" marker-end="url(#vcl-arrow)" />
      <path d="M 514 594 A 260 260 0 0 1 286 594" marker-end="url(#vcl-arrow)" />
      <path d="M 256 573 A 260 260 0 0 1 149 342" marker-end="url(#vcl-arrow)" />
    </g>

    <!-- INNER LOOP arcs: A -> B -> C -> D -> E -> A -->
    <g
      stroke="currentColor"
      stroke-width="1.5"
      fill="none"
      opacity="0.85"
    >
      <path d="M 474 434 A 85 85 0 0 1 390 451" marker-end="url(#vcl-arrow-thin)" />
      <path d="M 369 435 A 85 85 0 0 1 336 353" marker-end="url(#vcl-arrow-thin)" />
      <path d="M 342 325 A 85 85 0 0 1 407 269" marker-end="url(#vcl-arrow-thin)" />
      <path
        d="M 432 272 A 85 85 0 0 1 500 310"
        marker-end="url(#vcl-arrow-thin)"
        stroke-dasharray="4 3"
      />
      <path d="M 502 338 A 85 85 0 0 1 481 416" marker-end="url(#vcl-arrow-thin)" />
    </g>

    <!-- 4 -> A (entrada del loop interno desde el paso 4) -->
    <g stroke="currentColor" stroke-width="1.5" fill="none" opacity="0.85">
      <path
        d="M 635 375 Q 570 430 496 425"
        marker-end="url(#vcl-arrow-thin)"
      />
    </g>

    <!-- OUTER nodes -->
    <g>
      <circle cx="140" cy="360" r="24" fill="none" stroke="currentColor" stroke-width="2" />
      <text x="140" y="368" text-anchor="middle" font-size="22" font-weight="bold" fill="currentColor">1</text>
      <text x="100" y="352" text-anchor="end" font-size="16" fill="currentColor">
        <tspan x="100" dy="0">Separar</tspan>
        <tspan x="100" dy="20">subtarea</tspan>
      </text>

      <circle cx="270" cy="135" r="24" fill="none" stroke="currentColor" stroke-width="2" />
      <text x="270" y="143" text-anchor="middle" font-size="22" font-weight="bold" fill="currentColor">2</text>
      <text x="270" y="82" text-anchor="middle" font-size="16" fill="currentColor">
        <tspan x="270" dy="0">Iniciar conversación</tspan>
        <tspan x="270" dy="20">con IA</tspan>
      </text>

      <circle cx="530" cy="135" r="24" fill="none" stroke="currentColor" stroke-width="2" />
      <text x="530" y="143" text-anchor="middle" font-size="22" font-weight="bold" fill="currentColor">3</text>
      <text x="530" y="82" text-anchor="middle" font-size="16" fill="currentColor">
        <tspan x="530" dy="0">Crear plan</tspan>
        <tspan x="530" dy="20">con IA</tspan>
      </text>

      <circle cx="660" cy="360" r="24" fill="none" stroke="currentColor" stroke-width="2" />
      <text x="660" y="368" text-anchor="middle" font-size="22" font-weight="bold" fill="currentColor">4</text>
      <text x="700" y="332" text-anchor="start" font-size="16" fill="currentColor">
        <tspan x="700" dy="0">La IA ejecuta</tspan>
        <tspan x="700" dy="20">el plan</tspan>
      </text>

      <circle cx="530" cy="585" r="24" fill="none" stroke="currentColor" stroke-width="2" />
      <text x="530" y="593" text-anchor="middle" font-size="22" font-weight="bold" fill="currentColor">5</text>
      <text x="530" y="645" text-anchor="middle" font-size="16" fill="currentColor">
        <tspan x="530" dy="0">Probar y</tspan>
        <tspan x="530" dy="20">verificar</tspan>
      </text>

      <circle cx="270" cy="585" r="24" fill="none" stroke="currentColor" stroke-width="2" />
      <text x="270" y="593" text-anchor="middle" font-size="22" font-weight="bold" fill="currentColor">6</text>
      <text x="270" y="645" text-anchor="middle" font-size="16" fill="currentColor">
        <tspan x="270" dy="0">Refinar e</tspan>
        <tspan x="270" dy="20">iterar</tspan>
      </text>
    </g>

    <!-- INNER nodes (A..E) -->
    <g>
      <circle cx="490" cy="425" r="18" fill="none" stroke="currentColor" stroke-width="1.5" />
      <text x="490" y="431" text-anchor="middle" font-size="15" font-weight="bold" fill="currentColor">A</text>
      <text x="529" y="445" text-anchor="start" font-size="12" fill="currentColor">Escribir<tspan x="529" dy="14">código</tspan></text>

      <circle cx="380" cy="442" r="18" fill="none" stroke="currentColor" stroke-width="1.5" />
      <text x="380" y="448" text-anchor="middle" font-size="15" font-weight="bold" fill="currentColor">B</text>
      <text x="380" y="482" text-anchor="middle" font-size="12" fill="currentColor">Compilar</text>

      <circle cx="330" cy="340" r="18" fill="none" stroke="currentColor" stroke-width="1.5" />
      <text x="330" y="346" text-anchor="middle" font-size="15" font-weight="bold" fill="currentColor">C</text>
      <text x="293" y="344" text-anchor="end" font-size="12" fill="currentColor">Ejecutar</text>

      <circle cx="420" cy="260" r="18" fill="none" stroke="currentColor" stroke-width="1.5" />
      <text x="420" y="266" text-anchor="middle" font-size="15" font-weight="bold" fill="currentColor">D</text>
      <text x="420" y="232" text-anchor="middle" font-size="12" fill="currentColor">Probar</text>

      <circle cx="512" cy="324" r="18" fill="none" stroke="currentColor" stroke-width="1.5" />
      <text x="512" y="330" text-anchor="middle" font-size="15" font-weight="bold" fill="currentColor">E</text>
      <text x="552" y="318" text-anchor="start" font-size="12" fill="currentColor">Depurar</text>
    </g>
  </svg>
</template>
