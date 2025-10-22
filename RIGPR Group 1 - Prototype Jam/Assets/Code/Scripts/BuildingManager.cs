using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    [Header("Core References")]
    public Camera mainCamera;                     // Main camera reference
    public GridManager grid;                      // Grid snapping system
    public FreeCamera freeCam;                    // Reference to free camera controls
    public LayerMask buildSurfaceMask;            // Layer(s) that count as valid build surfaces
    
    // Determines which prefabs can use stacking, dragging, etc.
    public enum BuildCategory { Default, Wall, Shopfront, Decor, Runway }

    [System.Serializable]
    public class BuildPrefab
    {
        public GameObject prefab;                 // Prefab to spawn
        public BuildCategory category;            // Type of build (affects behaviour)
    }

    [Header("Prefabs & State")]
    public BuildPrefab[] buildPrefabs;            // All placeable prefabs (set in Inspector)
    private int selectedIndex = 0;                // Currently selected prefab index
    private GameObject previewObject;             // Ghost preview of what will be placed
    private Quaternion rotation = Quaternion.identity; // Current placement rotation
    private List<GameObject> placedObjects = new List<GameObject>(); // Track placed objects

    [Header("Height / Layering")]
    public float baseHeightStep = 3f;             // Default fallback for unknown prefab height
    private float currentHeight = 0f;             // Current placement height (Y offset)
    private float wallHeightStep = 20f;           // Step height per layer for walls
    private int currentLayer = 0;                 // Current vertical layer index

    [Header("Build Mode Settings")]
    public bool buildModeActive = false;          // Toggles building mode on/off
    public GameObject buildUI;                    // Optional UI shown during build mode

    [Header("Drag Build Settings")]
    private bool isDragging = false;              // True while dragging to place multiple pieces
    private Vector3 dragStartPos;                 // World position where drag started
    private Vector3 dragEndPos;                   // Current drag end position
    private List<GameObject> dragPreviewObjects = new List<GameObject>(); // Preview objects along drag
    public float wallSegmentLength = 20f;         // Default spacing between segments

    void Update()
    {
        // Toggle build mode with B
        if (Input.GetKeyDown(KeyCode.B))
        {
            buildModeActive = !buildModeActive;

            // Show/hide build UI
            if (buildUI != null)
                buildUI.SetActive(buildModeActive);

            // Remove preview when leaving build mode
            if (!buildModeActive && previewObject != null)
                Destroy(previewObject);
        }

        if (!buildModeActive) return;

        // Undo last placement (Ctrl + Z)
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z))
        {
            if (placedObjects.Count > 0)
            {
                GameObject last = placedObjects[placedObjects.Count - 1];
                placedObjects.RemoveAt(placedObjects.Count - 1);
                Destroy(last);
            }
        }

        // Handle all live build input
        HandleHeightAdjustment();
        HandlePreview();
        HandlePlacementInput();
        HandleRotationInput();
        HandleCycleInput();
        HandleDrag();
    }

    //  PREVIEW 
    // Shows a ghost preview following the mouse, snapped to the grid.
    void HandlePreview()
    {
        if (previewObject == null)
        {
            // Create preview version of current prefab
            if (buildPrefabs[selectedIndex]?.prefab == null) return;
            previewObject = Instantiate(buildPrefabs[selectedIndex].prefab);
            foreach (var col in previewObject.GetComponentsInChildren<Collider>())
                col.enabled = false; // disable colliders for preview
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 5000f, buildSurfaceMask))
        {
            Vector3 gridPos = grid.GetNearestPointOnGrid(hit.point);
            var cat = buildPrefabs[selectedIndex].category;

            // Set height based on category
            if (cat == BuildCategory.Wall || cat == BuildCategory.Shopfront || cat == BuildCategory.Decor)
                gridPos.y = currentHeight;
            else
                gridPos.y = 0f;

            // Update preview transform
            previewObject.transform.position = gridPos;
            previewObject.transform.rotation = rotation;
        }
    }

    //  SINGLE CLICK PLACEMENT
    void HandlePlacementInput()
    {
        // Skip single-click if currently dragging (prevents duplicate wall)
        if (isDragging) return;

        // Left click places a single prefab
        if (Input.GetMouseButtonDown(0))
        {
            GameObject newPiece = Instantiate(buildPrefabs[selectedIndex].prefab,
                                              previewObject.transform.position,
                                              rotation);
            placedObjects.Add(newPiece);
        }

        // Delete key removes selected building (if tagged)
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag("Building"))
                {
                    placedObjects.Remove(hit.collider.gameObject);
                    Destroy(hit.collider.gameObject);
                }
            }
        }
    }

    // ROTATION
    void HandleRotationInput()
    {
        if (Input.GetKeyDown(KeyCode.R))
            rotation *= Quaternion.Euler(0, 90, 0); // Rotate 90° each press
    }

    // PREFAB CYCLING
    void HandleCycleInput()
    {
        // Tab cycles forward
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Destroy(previewObject);
            selectedIndex = (selectedIndex + 1) % buildPrefabs.Length;
        }

        // Shift + Tab cycles backward
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Tab))
        {
            Destroy(previewObject);
            selectedIndex = (selectedIndex - 1 + buildPrefabs.Length) % buildPrefabs.Length;
        }
    }

    // HEIGHT LAYER CONTROL
    // Controls E/Q vertical stacking for modular pieces like walls or decor.
    void HandleHeightAdjustment()
    {
        var cat = buildPrefabs[selectedIndex].category;

        // Only layered categories use vertical offset
        if (cat != BuildCategory.Wall && cat != BuildCategory.Shopfront && cat != BuildCategory.Decor)
        {
            currentHeight = 0f;
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            currentLayer++;
            currentHeight = currentLayer * wallHeightStep;
            MoveCameraToHeight(currentHeight);
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            currentLayer = Mathf.Max(0, currentLayer - 1);
            currentHeight = currentLayer * wallHeightStep;
            MoveCameraToHeight(currentHeight);
        }
    }

    // CAMERA HEIGHT SYNC
    float GetCurrentPrefabHeight()
    {
        GameObject prefab = buildPrefabs[selectedIndex].prefab;
        Collider col = prefab.GetComponentInChildren<Collider>();
        return col ? col.bounds.size.y : baseHeightStep;
    }

    void MoveCameraToHeight(float height)
    {
        if (freeCam != null)
            freeCam.SetTargetHeight(height);
    }

    // DRAG BUILD LOGIC 
    // Allows click & drag placement for modular pieces (walls, runways, shopfronts).
    void HandleDrag()
    {
        var cat = buildPrefabs[selectedIndex].category;

        // Only certain categories support dragging
        if (cat != BuildCategory.Wall && cat != BuildCategory.Shopfront && cat != BuildCategory.Runway)
            return;

        // Raycast from camera to mouse position
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 5000f, buildSurfaceMask))
            return;

        Vector3 gridPos = grid.GetNearestPointOnGrid(hit.point);
        gridPos.y = currentHeight;

        // Start drag
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragStartPos = gridPos;
            dragEndPos = gridPos;
        }

        // While dragging, continuously update ghost preview line
        if (isDragging && Input.GetMouseButton(0))
        {
            dragEndPos = gridPos;
            UpdateDragPreview(dragStartPos, dragEndPos);
        }

        // Release mouse button to place line of objects
        if (isDragging && Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            PlaceDragWalls(dragStartPos, dragEndPos);
            ClearDragPreview();
        }
    }

    // Creates temporary ghost versions of prefabs along drag path.
    void UpdateDragPreview(Vector3 start, Vector3 end)
    {
        ClearDragPreview();

        start = grid.GetNearestPointOnGrid(start);
        end = grid.GetNearestPointOnGrid(end);

        Vector3 dir = (end - start);
        float distance = dir.magnitude;
        if (distance < 0.1f) return;

        dir.Normalize();
        Quaternion wallRot = Quaternion.LookRotation(dir, Vector3.up);

        float segLen = GetSegmentLengthForCategory(buildPrefabs[selectedIndex].category);
        int count = Mathf.CeilToInt(distance / segLen);
        Vector3 step = dir * segLen;

        for (int i = 0; i <= count; i++)
        {
            Vector3 pos = start + step * i;
            GameObject ghost = Instantiate(buildPrefabs[selectedIndex].prefab, pos, wallRot);
            foreach (var col in ghost.GetComponentsInChildren<Collider>())
                col.enabled = false;
            SetGhostMaterial(ghost, true);
            dragPreviewObjects.Add(ghost);
        }
    }

    // Deletes all active ghost previews.
    void ClearDragPreview()
    {
        foreach (var obj in dragPreviewObjects)
            Destroy(obj);
        dragPreviewObjects.Clear();
    }

    // Places actual objects along drag line (walls, shopfronts, runways, etc.)
    void PlaceDragWalls(Vector3 start, Vector3 end)
    {
        // Prevents rogue "first click" wall from being left behind
        if (placedObjects.Count > 0)
        {
            GameObject lastPlaced = placedObjects[placedObjects.Count - 1];
            if (lastPlaced != null && lastPlaced.CompareTag("Building"))
            {
                Destroy(lastPlaced);
                placedObjects.RemoveAt(placedObjects.Count - 1);
            }
        }

        start = grid.GetNearestPointOnGrid(start);
        end = grid.GetNearestPointOnGrid(end);

        Vector3 dir = (end - start);
        float distance = dir.magnitude;
        if (distance < 0.1f) return;

        dir.Normalize();
        Quaternion wallRot = Quaternion.LookRotation(dir, Vector3.up);

        float segLen = GetSegmentLengthForCategory(buildPrefabs[selectedIndex].category);
        int count = Mathf.CeilToInt(distance / segLen);
        Vector3 step = dir * segLen;

        for (int i = 0; i <= count; i++)
        {
            Vector3 pos = start + step * i;
            pos.y = currentHeight;

            GameObject newPiece = Instantiate(buildPrefabs[selectedIndex].prefab, pos, wallRot);
            placedObjects.Add(newPiece);
        }

        ClearDragPreview();
    }

    // Adjusts materials for preview objects (semi-transparent ghosts)
    void SetGhostMaterial(GameObject obj, bool isGhost)
    {
        Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
        foreach (var r in rends)
        {
            foreach (var mat in r.materials)
            {
                Color c = mat.color;
                c.a = isGhost ? 0.4f : 1f;
                mat.color = c;
            }
        }
    }

    // Returns segment spacing depending on category type.
    float GetSegmentLengthForCategory(BuildCategory cat)
    {
        switch (cat)
        {
            case BuildCategory.Runway:
                return 40f; // big pieces
            case BuildCategory.Shopfront:
                return 30f;
            case BuildCategory.Wall:
                return 20f;
            default:
                return wallSegmentLength;
        }
    }
}
