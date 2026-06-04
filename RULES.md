Para estructurar la lógica de este juego de forma clara y escalable (ideal para plantear la arquitectura de los turnos y las reglas), aquí tienes la explicación detallada de cómo funciona el Truco Venezolano, organizada como un flujo de estados y resolución de eventos.



1\. Estructura Base y Condiciones de Victoria

Jugadores: 2 o 4 (en equipos de 2).



Mazo: 40 cartas (baraja española sin 8s, 9s, ni comodines).



Meta Final: El primer jugador o equipo en alcanzar 12 o 24 puntos (piedras) gana la partida.



2\. Fase de Setup: El Reparto y La Vira

Al iniciar cada "mano" (ronda completa), se reparten 3 cartas a cada jugador. Inmediatamente se voltea una carta en la mesa, conocida como La Vira.

El palo de esta carta altera el estado del juego para esa mano, definiendo las dos cartas más poderosas:



Perico: Es el 11 (Caballo) del mismo palo de la Vira.



Perica: Es el 10 (Sota) del mismo palo de la Vira.



Excepción lógica: Si la Vira es un 11 o un 10, la carta especial correspondiente es reemplazada por el 12 (Rey) de ese mismo palo.



3\. Jerarquía de Cartas (De mayor a menor poder)

Para resolver quién gana cada enfrentamiento en la mesa, el sistema debe evaluar este orden estricto:



Perico



Perica



1 de Espada (Espadilla)



1 de Basto (Bastillo)



7 de Espada



7 de Oro



Cualquier 3



Cualquier 2



1 de Copa y 1 de Oro



12s, 11s, 10s (Que no sean Perico/Perica)



7 de Copa y 7 de Basto



6s, 5s, 4s



4\. Fase de Cantos Iniciales: Envido y Flor

Esta fase ocurre en la primera de las tres rondas, antes o en el mismo momento en que los jugadores lanzan su primera carta. Son sistemas de puntuación paralelos al juego de las cartas.



La Flor (Prioridad Alta)



Condición: Tener 3 cartas del mismo palo, o 2 del mismo palo + Perico o Perica. "Flor reservada" es tener Perico + Perica + cualquier carta.



Mecánica: Si alguien tiene Flor, el Envido queda anulado. Una flor normal da 3 puntos. La flor reservada da 5 puntos (3 de flor + 1 de envido + 1 de truco automático).



Cantos habilitados: Flor, A ley (usado para insinuar flor sin cantarla directamente), Con flor quiero, Con flor no quiero.



El Envido (Prioridad Media)



Condición: Si nadie canta Flor, se disputa el Envido. Evalúa quién tiene el par de cartas del mismo palo con mayor valor matemático.



Cálculo de puntos: Se suman los valores de las dos cartas del mismo palo + 20 de base. (Figuras 10, 11 y 12 valen 0).



Modificadores: El Perico suma 30 fijos y la Perica 29 fijos a cualquier otra carta que se tenga.



Cantos habilitados: Envido, Quiero, No quiero, Quiero y envido, Quiero y \[cantidad] piedras más, Envido la falta / Falta envido.



Resolución: Si se rechaza ("No quiero"), el que cantó se lleva 1 punto o los acumulados hasta el canto anterior. Si se acepta ("Quiero"), se muestran los puntos y el mayor gana la apuesta.



5\. Fase de Resolución: El Truco (Las 3 Rondas)

Es el núcleo (core loop) de jugar las cartas sobre la mesa. Consiste en 3 enfrentamientos o "manos".



Objetivo: Ganar 2 de las 3 rondas lanzando la carta de mayor jerarquía.



Escalamiento de Apuestas: El truco base vale 1 punto (si no se canta nada). Durante su turno, un jugador puede escalar el valor de la mano usando los cantos de Truco.



Cantos habilitados: Truco (sube a 3 pts) -> Retruco / Quiero y retruco (sube a 6 pts) -> Vale 9 / Quiero y vale nueve (sube a 9 pts) -> Vale juego / Quiero y vale juego (decide la partida entera).



Respuestas: Quiero (se acepta jugar por esos puntos), No quiero (la mano termina inmediatamente, el rival se lleva los puntos acumulados hasta antes de ese canto).



Manejo de Empates ("Empardes"):



Si se empata en la 1ra ronda: No se juega la 2da. Se decide todo lanzando la carta más alta en la 3ra ronda.



Si persiste el empate en la 3ra ronda: Gana el jugador que fue "Mano" (el que lanzó la carta primero al inicio de la partida).



6\. Estados Especiales y Comunicación

Prive: Es un estado de partida. Ocurre cuando a un equipo le falta exactamente 1 punto para ganar el juego completo. Se canta para evaluar quién gana si ambos equipos están a un punto de la victoria (evaluando los puntos de la misma forma que un envido).



Barajo: Estado de error o penalización. Se canta si hubo un mal reparto o irregularidad, castigando con 2 puntos al infractor y reiniciando la mano.



Señas Verbales Tácticas: Se usan en partidas 2v2 para coordinar qué carta lanzar sin mostrar la mano.



Voy / Ven a mí: Instruye al compañero a jugar una carta de valor bajo.



Me quedo / Quédate: Instruye al compañero a jugar una carta de valor alto para asegurar la ronda.

