using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class GameManager : MonoBehaviour
{
    [Header("Configuracion inicial (no cambia en ejecucion)")]
    [FormerlySerializedAs("width")]
    public int ancho = 50;

    [FormerlySerializedAs("height")]
    public int alto = 30;

    [FormerlySerializedAs("tiempoEntreGeneraciones")]
    [FormerlySerializedAs("updateTime")]
    public float tiempoEntrePasos = 0.1f;

    public float tiempoEntreArenaAutomatica = 0.15f;

    [Header("Colores de la textura")]
    public Color colorArena = new Color(0.76f, 0.60f, 0.30f, 1f);
    public Color colorVacio = new Color(0.96f, 0.94f, 0.90f, 1f);

 
    // true = hay arena, false = vacio.
    private bool[,] grilla;
    private float temporizador;
    private float temporizadorArenaAutomatica;
    private int generacionActual;
    private int columnaSeleccionada = -1;
    private bool simulacionAutomatica = false;
    private bool isPaused = false;
    private Texture2D textura;

    void Start()
    {
        // Los arrays se reservan una sola vez, nunca dentro del bucle.
        grilla = new bool[ancho, alto];
        generacionActual = 0;
        columnaSeleccionada = -1;
        simulacionAutomatica = false;

        // Este script no lee teclas: escucha los eventos del InputManager.
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnPause += TogglePause;
            InputManager.Instance.OnRestart += RestartSimulation;
            InputManager.Instance.OnToggleCell += PonerArenaEnMouse;
            InputManager.Instance.OnAutoSim += AlternarAutomatica;
        }

        BuildTexture();
        ClearGrid();
    }

    void Update()
    {
        if (InputManager.Instance != null && InputManager.Instance.EstaPintando)
            PonerArenaEnMouse();

        UpdateVisuals();

        if (isPaused) return;

        temporizador += Time.deltaTime;
        if (temporizador >= tiempoEntrePasos)
        {
            SoltarArenaDesdeArriba();
            Step();
            MostrarResumenConsola();
            temporizador = 0f;
        }
    }

    // Tecla P. Congela el avance.
    void TogglePause()
    {
        isPaused = !isPaused;
        Debug.Log(isPaused ? "Simulación pausada" : "Simulación reanudada");
    }

    // Clic izquierdo: pone arena donde apunta el mouse.
    void PonerArenaEnMouse()
    {
        Vector3 worldPos;
        if (Mouse.current != null)
            worldPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        else
            worldPos = Camera.main.transform.position;

        int x = Mathf.FloorToInt(worldPos.x);
        int y = Mathf.FloorToInt(worldPos.y);

        if (x < 0 || x >= ancho || y < 0 || y >= alto) return;

        grilla[x, y] = true; // true = arena
        columnaSeleccionada = x;
    }

    // Clic derecho: prende o apaga la arena automatica.
    void AlternarAutomatica()
    {
        simulacionAutomatica = !simulacionAutomatica;
        Debug.Log(simulacionAutomatica ? "Simulación automática: ON" : "Simulación automática: OFF");
    }

    // Tecla R. Deja el tablero vacio.
    void RestartSimulation()
    {
        Debug.Log("Reiniciando simulación...");
        ClearGrid();
        columnaSeleccionada = -1;
        simulacionAutomatica = false;
        generacionActual = 0;
        temporizador = 0f;
    }

    // Un unico sprite para toda la grilla.
    void BuildTexture()
    {
        textura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        textura.filterMode = FilterMode.Point;
        textura.wrapMode = TextureWrapMode.Clamp;

        Sprite sprite = Sprite.Create(
            textura,
            new Rect(0, 0, ancho, alto),
            Vector2.zero,
            1f);

        SpriteRenderer rend = GetComponent<SpriteRenderer>();
        if (rend == null) rend = gameObject.AddComponent<SpriteRenderer>();
        rend.sprite = sprite;
    }

    // Deja todas las celdas en false (vacias).
    public void ClearGrid()
    {
        for (int x = 0; x < ancho; x++)
        {
            for (int y = 0; y < alto; y++)
            {
                grilla[x, y] = false;
            }
        }
        UpdateVisuals();
    }

    // Cada grano mira abajo, abajo-izquierda y abajo-derecha.
    // Recorremos de ABAJO hacia ARRIBA para que cada grano baje una sola casilla por tick.
    void Step()
    {
        generacionActual++;

        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < ancho; x++)
            {
                if (!grilla[x, y]) continue; // false = vacio, no hay nada que mover

                int abajoY = y - 1;
                int abajoX = x;
                int diagIzqX = x - 1;
                int diagDerX = x + 1;

                bool abajoLibre = EstaVacia(abajoX, abajoY);
                bool izqLibre = EstaVacia(diagIzqX, abajoY);
                bool derLibre = EstaVacia(diagDerX, abajoY);

                if (abajoLibre)
                {
                    MoverArena(x, y, abajoX, abajoY);
                    continue;
                }

                if (izqLibre && derLibre)
                {
                    if (Random.value < 0.5f)
                        MoverArena(x, y, diagIzqX, abajoY);
                    else
                        MoverArena(x, y, diagDerX, abajoY);
                    continue;
                }

                if (izqLibre)
                {
                    MoverArena(x, y, diagIzqX, abajoY);
                    continue;
                }

                if (derLibre)
                {
                    MoverArena(x, y, diagDerX, abajoY);
                    continue;
                }
            }
        }
    }

    void SoltarArenaDesdeArriba()
    {
        if (!simulacionAutomatica) return;

        temporizadorArenaAutomatica += tiempoEntrePasos;
        if (temporizadorArenaAutomatica < tiempoEntreArenaAutomatica) return;
        temporizadorArenaAutomatica = 0f;

        int columna = Random.Range(0, ancho);
        int arriba = alto - 1;
        if (EstaVacia(columna, arriba))
            grilla[columna, arriba] = true;
    }

    // Pinta cada casilla: true = colorArena, false = colorVacio.
    void UpdateVisuals()
    {
        if (textura == null) return;

        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < ancho; x++)
            {
                Color color = grilla[x, y] ? colorArena : colorVacio;
                textura.SetPixel(x, y, color);
            }
        }

        textura.Apply();
    }

    // Si la casilla no existe (piso o pared), cuenta como ocupada
    // para que la arena no se salga del tablero.
    bool EstaVacia(int x, int y)
    {
        if (x < 0 || x >= ancho || y < 0 || y >= alto) return false;
        return !grilla[x, y];
    }

    void MoverArena(int origenX, int origenY, int destinoX, int destinoY)
    {
        grilla[origenX, origenY] = false;
        grilla[destinoX, destinoY] = true;
    }

    void MostrarResumenConsola()
    {
        int particulasActivas = 0;
        for (int x = 0; x < ancho; x++)
        {
            for (int y = 0; y < alto; y++)
            {
                if (grilla[x, y]) particulasActivas++;
            }
        }

        Debug.Log($"[Generación {generacionActual}] Partículas de arena activas: {particulasActivas}");
    }
}
