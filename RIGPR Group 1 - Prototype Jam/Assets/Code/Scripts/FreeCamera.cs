using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class FreeCamera : MonoBehaviour
{
    // Variables for camera speeds 
    [Header("Movement")]
    public float moveSpeed = 50f;
    public float fastMoveMultiplier = 2f;
    public float rotationSpeed = 300f;
    public float panSpeed = 5f;
    public float zoomSpeed = 1500f;
    public float smooth = 0.1f;

    // limits for zooming

    [Header("Height Limits")]
    public float minY = 5f;
    public float maxY = 120f;

    private Vector3 targetPosition;
    private Vector3 moveVelocity;

    private Camera cam;
    private Vector3 lastMousePosition;
    private Vector3 defaultPosition;
    private Quaternion defaultRotation;

    private bool isAutoPanning = false;
    void Start()
    {
        cam = GetComponent<Camera>();
        targetPosition = transform.position;
        defaultPosition = transform.position;
        defaultRotation = transform.rotation;
    }

    void Update()
    {
    // Only allow manual input when NOT auto-panning
    if (!isAutoPanning)
        {
            HandleMovement();
            HandleMouseControls();
            HandleZoom();
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * panSpeed);

            // stop panning when close enough
            if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
                isAutoPanning = false;
        }
    }

    public void ResetToDefaultView()
    {
        StartCoroutine(RecenterCamera());
    }

    private IEnumerator RecenterCamera()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        float elapsed = 0f;
        float duration = 1f; // 1 second pan

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, defaultPosition, elapsed / duration);
            transform.rotation = Quaternion.Slerp(startRot, defaultRotation, elapsed / duration);
            yield return null;
        }
    }
    void HandleMovement()
    {
        Vector3 input = Vector3.zero;

        // Keyboard WASD or arrow key movement 
        if (Input.GetKey(KeyCode.W)) input += transform.forward;
        if (Input.GetKey(KeyCode.S)) input -= transform.forward;
        if (Input.GetKey(KeyCode.A)) input -= transform.right;
        if (Input.GetKey(KeyCode.D)) input += transform.right;

        input.y = 0; // Prevent vertical drift
        input.Normalize();

        float currentSpeed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMoveMultiplier : 1f);
        targetPosition += input * currentSpeed * Time.deltaTime;

        // Clamp camera height
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, maxY);

        // Smooth interpolation for camera movement
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref moveVelocity, smooth);
    }

    void HandleMouseControls()
    {
        // Rotate camera when holding right mouse button
        if (Input.GetMouseButton(1))
        {
            float rotX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
            float rotY = -Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;

            transform.eulerAngles += new Vector3(rotY, rotX, 0);
        }

        // Pan camera with middle mouse
        if (Input.GetMouseButtonDown(2))
            lastMousePosition = Input.mousePosition;

        if (Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            targetPosition -= transform.right * delta.x * panSpeed * Time.deltaTime;
            targetPosition -= transform.up * delta.y * panSpeed * Time.deltaTime;
            lastMousePosition = Input.mousePosition;
        }
    }

    // Zoom camera with ScrollWheel
    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetPosition += transform.forward * scroll * zoomSpeed * Time.deltaTime;
        }
    }

    public void SetTargetHeight(float height)
    {
        targetPosition = new Vector3(transform.position.x, height + 20f, transform.position.z);
        isAutoPanning = true;
    }


}
