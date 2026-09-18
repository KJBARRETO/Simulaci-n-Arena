using UnityEngine;
using System;


public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private PlayerController controls;

    // Eventos de cámara
    public event Action<Vector2> OnCameraMove;
    public event Action<float> OnCameraZoom;

    // Eventos de gameplay
    public event Action OnPause;
    public event Action OnRestart;
    public event Action OnClear;
    public event Action OnToggleCell;
    public event Action OnAutoSim;

    // True mientras el izquierdo (ToggleCell) esta apretado.
    // Igual que la camara guarda el stick: el Input System dice si sigue presionado.
    public bool EstaPintando =>
        controls != null && controls.Gameplay.ToggleCell.IsPressed();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        controls = new PlayerController();

        // Cámara
        controls.Camera.Move.performed += ctx => OnCameraMove?.Invoke(ctx.ReadValue<Vector2>());
        controls.Camera.Move.canceled += ctx => OnCameraMove?.Invoke(Vector2.zero);
        controls.Camera.Zoom.performed += ctx => OnCameraZoom?.Invoke(ctx.ReadValue<float>());
        controls.Camera.Zoom.canceled += ctx => OnCameraZoom?.Invoke(0);

        controls.Gameplay.Pause.performed += _ => OnPause?.Invoke();
        controls.Gameplay.Restart.performed += _ => OnRestart?.Invoke();
        controls.Gameplay.Clear.performed += _ => OnClear?.Invoke();
        controls.Gameplay.ToggleCell.performed += _ => OnToggleCell?.Invoke();

        var autoSim = controls.asset.FindActionMap("Gameplay").FindAction("AutoSim");
        if (autoSim != null)
            autoSim.performed += _ => OnAutoSim?.Invoke();
    }

    void OnEnable()
    {
        controls.Camera.Enable();
        controls.Gameplay.Enable();
    }

    void OnDisable()
    {
        controls.Camera.Disable();
        controls.Gameplay.Disable();
    }
}
