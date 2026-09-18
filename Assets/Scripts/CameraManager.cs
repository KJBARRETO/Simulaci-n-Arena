using UnityEngine;
using UnityEngine.InputSystem;

public class CameraManager : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 100f;

    [Header("Zoom")]
    public float zoomSpeed = 100f;
    public float minZoom = 2f;
    public float maxZoom = 30f;

   
    [Header("Tamaño del tablero")]
    public float anchoVista = 80f;
    public float altoVista = 60f;

    private Camera cam;
    private Vector2 moveInput = Vector2.zero;
    private float zoomInput = 0f;

    void Start()
    {
        cam = Camera.main;

       
        maxZoom = altoVista * 0.5f;
        cam.transform.position = new Vector3(anchoVista * 0.5f, altoVista * 0.5f, cam.transform.position.z);
        cam.orthographicSize = maxZoom;

        InputManager.Instance.OnCameraMove += val => moveInput = val;
        InputManager.Instance.OnCameraZoom += val => zoomInput = val;
    }

    void Update()
    {
        AjustarRecortePantalla();
        HandleMovement();
        HandleZoom();
    }

   
    void AjustarRecortePantalla()
    {
        if (cam == null) return;

        float objetivo = anchoVista / altoVista;
        float ventana = (float)Screen.width / Mathf.Max(1, Screen.height);
        if (ventana > objetivo)
        {
            float w = objetivo / ventana;
            cam.rect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        else
        {
            float h = ventana / objetivo;
            cam.rect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
    }

    void HandleMovement()
    {
        Vector3 delta = new Vector3(moveInput.x, moveInput.y, 0f);
        cam.transform.position += delta * moveSpeed * Time.deltaTime;
    }

    void HandleZoom()
    {
        cam.orthographicSize -= zoomInput * zoomSpeed * Time.deltaTime;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
    }
}
