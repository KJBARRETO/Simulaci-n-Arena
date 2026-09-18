using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Cada casilla del tablero solo puede estar en uno de estos dos estados:
// vacia (no hay nada) o con arena.
public enum EstadoCelda
{
    Vacia,
    Arena
}

public class GameManager : MonoBehaviour
{
    // COSAS QUE CONFIGURAS ANTES DE JUGAR 

    [Header("Configuracion inicial (no cambia en ejecucion)")]
    [FormerlySerializedAs("width")]
    public int ancho = 50;   // casillas de izquierda a derecha

    [FormerlySerializedAs("height")]
    public int alto = 30;    // casillas de abajo hacia arriba

    // Que tan rapido cae la arena.
    // 0.1 = espera 0.1 segundos entre cada caida.
    // No es gravedad de verdad: solo es "cada cuanto avanza un pasito".
    [FormerlySerializedAs("tiempoEntreGeneraciones")]
    [FormerlySerializedAs("updateTime")]
    public float tiempoEntrePasos = 0.1f;

    // Si nadie hace click, cada tanto aparece arena sola
    // para que el tablero no se vea vacio.
    public float tiempoEntreArenaAutomatica = 0.15f;

    [Header("Colores de la textura")]
    public Color colorArena = new Color(0.76f, 0.60f, 0.30f, 1f); // color de la arena
    public Color colorVacio = new Color(0.96f, 0.94f, 0.90f, 1f); // color del fondo

    // COSAS QUE VAN CAMBIANDO MIENTRAS JUEGA
    // El tablero. Cada posicion [columna, fila] dice si hay arena o no.
    private EstadoCelda[,] grilla;

    // Reloj chiquito. Suma tiempo hasta que llega a tiempoEntrePasos
    // y ahi recien deja caer la arena un poco.
    private float temporizador;

    // Otro reloj, para soltar arena sola cuando no hay click.
    private float temporizadorArenaAutomatica;

    // Cuantas veces ya avanzo la simulacion. 
    private int generacionActual;

    // De que columna esta cayendo la arena.
    // -1 = todavia no clickeaste, entonces se elige una al azar.
    // Si clickeas con el izquierdo, se guarda el numero de esa columna.
    private int columnaSeleccionada = -1;

    // Click derecho: enciende o apaga la lluvia automatica de arena.
    private bool simulacionAutomatica = false;

 
    private Texture2D textura;

    // START se ejecuta UNA vez, cuando apretas Play
    // En Conway: armaba el dibujo y ponia celulas vivas al azar.
    // Aca: arma el dibujo igual, deja el tablero vacio y tira
    // un granito de arena desde arriba para que se vea algo.
    void Start()
    {
        // Creamos el tablero con el ancho y alto que pusimos arriba.
        grilla = new EstadoCelda[ancho, alto];
        generacionActual = 0;
        columnaSeleccionada = -1;
        simulacionAutomatica = false;

        // Solo se suscribe a los eventos del InputManager.
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPause += AlternarPausa;
            InputManager.Instance.OnRestart += ReiniciarSimulacion;
            InputManager.Instance.OnClear += LimpiarSimulacion;
            InputManager.Instance.OnToggleCell += PonerArenaEnMouse;
            InputManager.Instance.OnAutoSim += AlternarAutomatica;
        }
        ConstruirTextura();

        // Todas las casillas empiezan vacias. No aparece arena hasta que el usuario clickee.
        VaciarGrilla();
    }
    // UPDATE  se ejecuta TODO el tiempo, muchas veces por segundo
    // Hace 3 cosas, siempre en este orden:
    // 1) mira si estas haciendo click
    // 2) dibuja el tablero
    // 3) de vez en cuando deja caer la arena un pasito
    void Update()
    {
        // 1) Si el InputManager dice que el izquierdo sigue apretado, seguimos pintando.
        if (InputManager.Instance != null && InputManager.Instance.EstaPintando)
            PonerArenaEnMouse();

        // 2) Pintamos el tablero tal como esta ahora.
        DibujarGrilla();

        // 3) Esperamos un poquito y recien ahi movemos la arena.
        //    Asi no cae a mil por hora. Si se baja  tiempoEntrePasos, cae mas rapido.
        temporizador += Time.deltaTime; 
        if (temporizador >= tiempoEntrePasos)
        {
            // Solo suelta arena nueva si hay click izquierdo o modo automatico (derecho).
            SoltarArenaDesdeArriba();

            // Mueve todos los granos un casillero, si pueden.
            Step();

            // Escribe en la consola cuantos granos hay.
            MostrarResumenConsola();

            // Reiniciamos el reloj para esperar el proximo pasito.
            temporizador = 0f;
        }
    }
    // STEP  aca es donde la arena CAE
   
    // En Conway, para cada casilla se miraban las 8 de alrededor
    // (arriba, abajo, costados y esquinas) y se CONTABAN cuantas
    // estaban vivas. Con ese numero se decidia si nacia o moria.
   
    // Aca tambien miramos las casillas de alrededor, pero no
    // contamos nada. De esas 8 solo nos importan 3 con esas 3 decidimos a donde se mueve el grano.
    void Step()
    {
        generacionActual++;

        // Empiezo por el piso (y = 0) y voy subiendo.
        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < ancho; x++)
            {
                // Si esta casilla esta vacia, no hay nada que mover.
                if (grilla[x, y] != EstadoCelda.Arena) continue;

                // Las 3 casillas que nos importan:
                // una justo abajo, una en diagonal a la izquierda
                // y una en diagonal a la derecha.
                int abajoY = y - 1;
                int abajoX = x;
                int diagIzqX = x - 1;
                int diagDerX = x + 1;

                // Antes de mirar una casilla, hay que preguntar si existe. Si el grano ya esta en el piso,
                // "abajo" quedaria fuera del tablero y el juego se romperia.
                // Si esta fuera, la tratamos como ocupada la arena no puede atravesar el piso ni las paredes.
                bool abajoLibre = EstaVacia(abajoX, abajoY);
                bool izqLibre = EstaVacia(diagIzqX, abajoY);
                bool derLibre = EstaVacia(diagDerX, abajoY);

                // si abajo no hay nada, cae derecho.
                if (abajoLibre)
                {
                    MoverArena(x, y, abajoX, abajoY);
                    continue; // este grano ya se movio, pasamos al siguiente
                }

              // abajo hay algo, entonces intenta resbalar de costado.
                // Si puede ir a los dos lados, tira una moneda (mitad y mitad).
                if (izqLibre && derLibre)
                {
                    if (Random.value < 0.5f)
                        MoverArena(x, y, diagIzqX, abajoY);
                    else
                        MoverArena(x, y, diagDerX, abajoY);
                    continue;
                }

                // Solo puede ir a la izquierda.
                if (izqLibre)
                {
                    MoverArena(x, y, diagIzqX, abajoY);
                    continue;
                }

                // Solo puede ir a la derecha.
                if (derLibre)
                {
                    MoverArena(x, y, diagDerX, abajoY);
                    continue;
                }

                //las tres casillas de abajo estan ocupadas.
                // El grano se queda quieto. Asi se va apilando
            }
        }
    }

    // CLICK elige desde que COLUMNA cae la arena
   
    void PonerArenaEnMouse()
    {
        Vector3 posicionMundo;
        if (Mouse.current != null)
            posicionMundo = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        else
            posicionMundo = Camera.main.transform.position;

        int x = Mathf.FloorToInt(posicionMundo.x);
        int y = Mathf.FloorToInt(posicionMundo.y);

        if (x < 0 || x >= ancho || y < 0 || y >= alto) return;

        grilla[x, y] = EstadoCelda.Arena;
        columnaSeleccionada = x;
    }

    // Click derecho (AutoSim): prende o apaga la Arena automatica.
    void AlternarAutomatica()
    {
        simulacionAutomatica = !simulacionAutomatica;
        Debug.Log(simulacionAutomatica ? "Simulaci?n autom?tica: ON" : "Simulaci?n autom?tica: OFF");
    }

    // Pone un grano nuevo en el techo de una columna.
    // Si esa casilla de arriba ya tiene arena, no pone otro encima.
    void SoltarArenaDesdeArriba()
    {
     
        if (!simulacionAutomatica) return;

        temporizadorArenaAutomatica += tiempoEntrePasos;
        if (temporizadorArenaAutomatica < tiempoEntreArenaAutomatica) return;
        temporizadorArenaAutomatica = 0f;

        int columna = Random.Range(0, ancho);
        int arriba = alto - 1;
        if (EstaVacia(columna, arriba))
        {
            grilla[columna, arriba] = EstadoCelda.Arena;
        }
    }

    // Recorre el tablero y pinta cada casilla
    void DibujarGrilla()
    {
        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < ancho; x++)
            {
                Color color = grilla[x, y] == EstadoCelda.Arena ? colorArena : colorVacio;
                textura.SetPixel(x, y, color); // pinta ese puntito
            }
        }

       
        textura.Apply();
    }

    // Esta casilla existe Y esta vacia?
    // Primero miramos si esta dentro del tablero.
    // Si preguntas grilla[-1, 0] el juego se rompe, por eso el orden importa.
    bool EstaVacia(int x, int y)
    {
        if (x < 0 || x >= ancho || y < 0 || y >= alto) return false;
        return grilla[x, y] == EstadoCelda.Vacia;
    }

    // El grano desaparece de aca y aparece alla.
    void MoverArena(int origenX, int origenY, int destinoX, int destinoY)
    {
        grilla[origenX, origenY] = EstadoCelda.Vacia;
        grilla[destinoX, destinoY] = EstadoCelda.Arena;
    }

    // Crea la imagen del tablero.
    // Cada casilla es un puntito, y el puntito de la esquina
    // de abajo a la izquierda es la casilla (0, 0).
    void ConstruirTextura()
    {
        textura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        textura.filterMode = FilterMode.Point; // puntitos nitidos, no borrosos
        textura.wrapMode = TextureWrapMode.Clamp;

        Sprite sprite = Sprite.Create(
            textura,
            new Rect(0, 0, ancho, alto),
            Vector2.zero,
            1f);

        SpriteRenderer renderizador = GetComponent<SpriteRenderer>();
        if (renderizador == null) renderizador = gameObject.AddComponent<SpriteRenderer>();
        renderizador.sprite = sprite;
    }

    // Deja todas las casillas vacias. El tablero queda limpio.
    void VaciarGrilla()
    {
        for (int x = 0; x < ancho; x++)
        {
            for (int y = 0; y < alto; y++)
            {
                grilla[x, y] = EstadoCelda.Vacia;
            }
        }
    }

    // Tecla P: pausa o sigue.
    void AlternarPausa()
    {
        enabled = !enabled;
        Debug.Log(enabled ? "Simulaci?n reanudada" : "Simulaci?n pausada");
    }

    // Tecla E: borra toda la arena.
    void LimpiarSimulacion()
    {
        VaciarGrilla();
        columnaSeleccionada = -1;
        simulacionAutomatica = false;
        generacionActual = 0;
        temporizador = 0f;
        Debug.Log("Limpiando simulaci?n...");
    }

    // Tecla R: borra todo y tira un grano nuevo desde arriba.
    void ReiniciarSimulacion()
    {
        VaciarGrilla();
        columnaSeleccionada = -1;
        simulacionAutomatica = false;
        generacionActual = 0;
        temporizador = 0f;
        Debug.Log("Reiniciando simulaci?n...");
    }

    // Escribe en la consola: generacion N y cuantos granos hay.
   
    void MostrarResumenConsola()
    {
        int particulasActivas = 0;
        for (int x = 0; x < ancho; x++)
        {
            for (int y = 0; y < alto; y++)
            {
                if (grilla[x, y] == EstadoCelda.Arena) particulasActivas++;
            }
        }

        Debug.Log($"[Generaci?n {generacionActual}] Part?culas de arena activas: {particulasActivas}");
    }
}
