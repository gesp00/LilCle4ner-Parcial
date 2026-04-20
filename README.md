(Texto creado con IA) (era tarde profe entendeme xD)

Documento de Proyecto: LIL´ CL34N3R

1. Información General
Nombre del juego: LIL´ CL34N3R

Género: Sigilo / Acción Isométrica.

Objetivo: Controlás a CLEAN-7, un robot limpiador en una estación espacial tomada por piratas. Debés recolectar celdas de energía y limpiar residuos radiactivos sin ser destruido por las unidades de seguridad enemigas. El nivel se completa al limpiar todos los objetivos de la estación.

2. Sistemas de Inteligencia Artificial (Entrega 1)
Para esta instancia, se implementó un sistema de IA basado en una Máquina de Estados Finitos (FSM) y sistemas de percepcion:

Percepción (Line of Sight): Los enemigos cuentan con un cono de visión real (LoS) que detecta al jugador basándose en distancia, ángulo y obstrucciones físicas (paredes).

Toma de Decisión (FSM): Los agentes cambian su comportamiento dinámicamente entre los siguientes estados:

Patrulla (Patrol): Movimiento autónomo entre puntos de control (Waypoints) usando el sistema de navegación NavMesh.

Persecución (Chase): Al detectar al jugador mediante el LoS, el enemigo aumenta su velocidad y lo persigue activamente.

Ataque (Attack): Al estar a una distancia mínima del jugador, el enemigo inicia su secuencia de ataque.

3. Estética y Coherencia Visual
El proyecto sigue un estilo de arte low poly 3D.

5. Controles Básicos
Movimiento: Teclas W, A, S, D (Orientación relativa a la cámara isométrica).

Interacción: Tecla E (Para limpiar basura o tomar celdas de energía).

Cámara: Vista isométrica fija con seguimiento suave del personaje.
