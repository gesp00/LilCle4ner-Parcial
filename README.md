(Texto creado con IA) (era tarde profe entendeme xD)

Documento de Proyecto: LIL´ CL34N3R

1. Información General
Nombre del juego: LIL´ CL34N3R

Género: Sigilo / Acción Isométrica.

Objetivo: Controlás a CLEAN-7, un robot limpiador en una estación espacial tomada por piratas. Debés recolectar celdas de energía y limpiar residuos radiactivos sin ser destruido por las unidades de seguridad enemigas. El nivel se completa al limpiar todos los objetivos de la estación.

2. Arquitectura General de IA (Entrega 2)
La inteligencia artificial del proyecto presenta una integración real entre las decisiones lógicas, el cálculo de rutas y el desplazamiento físico.

Toma de Decisiones: Implementada mediante Máquinas de Estados Finitos (FSM) que evalúan la información de los sistemas de percepción (Line of Sight) para determinar la acción actual.
Toma de Decisión (FSM): Los agentes cambian su comportamiento dinámicamente entre los siguientes estados:

Pathfinding: Se implementó el algoritmo $A^*$ para resolver de forma global por dónde conviene ir dentro del mapa.

Steering Behaviors: Se utilizan para resolver cómo se desplaza localmente el agente, otorgando movimientos más fluidos y reacciones dinámicas.

3. Sistemas implementados por Agente
El proyecto cuenta con 3 unidades enemigas que actúan y reaccionan al jugador de manera completamente distinta:

Enemigo 1: Guardia Básico (Unidad Estándar)

  Decisiones (FSM): Patrol, Chase, Attack.

  Percepción: Line of Sight (LoS) con comprobación de distancia, ángulo y obstrucciones.

  Movimiento: Navegación autónoma tradicional para patrullajes fijos y persecución directa.

Enemigo 2: Pirate Wanderer (Patrullero Errático)
  
  Decisiones (FSM): Wander, Intercept, Attack.
  
  Pathfinding: Utiliza A* para calcular rutas hacia destinos generados aleatoriamente en el mapa.  
  
  Steering Behaviors: Aplica Wander, que luego se combina con Arrive para desplazarse orgánicamente entre los nodos del camino. Al detectar al jugador, utiliza Seek con predicción (Intercept) para cortarle el paso,          finalmente usando Attack para aplicar el ataque y el daño.

4. Navegación y Entorno:

   El nivel está diseñado con paredes y pasillos que funcionan como restricciones espaciales concretas.
   Estos obstáculos bloquean las líneas de visión y hacen estrictamente necesaria la utilización del pathfinding para que los agentes puedan sortear el entorno arquitectónico y alcanzar sus objetivos.

6. Controles Básicos
   
Movimiento: Teclas W, A, S, D (Orientación relativa a la cámara isométrica).

Interacción: Tecla E (Para limpiar basura o tomar celdas de energía).

Cámara: Vista isométrica fija con seguimiento suave del personaje.
