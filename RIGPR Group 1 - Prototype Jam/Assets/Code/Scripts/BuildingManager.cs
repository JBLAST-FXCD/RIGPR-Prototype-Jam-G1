using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    [Header("Core References")]
    public Camera mainCamera;
    public GridManager grid;
    public FreeCamera freeCam;
    public LayerMask buildSurfaceMask;

    public enum BuildCategory { Default, Wall, Shopfront, Decor, Runway }

    [System.Serializable]
    public class BuildPrefab
    {
        public GameObject prefab;
        public BuildCategory category;
    }

    // Track mouse-drag rotation while holding R
    private bool rotating = false;
    private float currentRotationY = 0f;
    private Vector3 lastMousePos;
    private Vector3 lockedPreviewPos;

    [Header("Prefabs & State")]
    public BuildPrefab[] buildPrefabs;
    private int selectedIndex = 0;
    private GameObject previewObject;
    private Quaternion rotation = Quaternion.identity;
    private List<GameObject> placedObjects = new List<GameObject>();

    [Header("Height / Layering")]
    public float wallHeightStep = 20f;
    private float currentHeight = 0f;
    private int currentLayer = 0;

    [Header("Floor Generation")]
    public GameObject floorPrefab;

    [Header("Build Mode Settings")]
    public bool buildModeActive = false;
    public GameObject buildUI;

    [Header("Drag Build Settings")]
    private bool isDragging = false;
    private Vector3 dragStartPos;
    private Vector3 dragEndPos;
    private List<GameObject> dragPreviewObjects = new List<GameObject>();

    void Update()
    {
        // Toggle Build Mode
        if (Input.GetKeyDown(KeyCode.B))
        {
            buildModeActive = !buildModeActive;
            if (buildUI) buildUI.SetActive(buildModeActive);
            if (!buildModeActive && previewObject) Destroy(previewObject);
        }

        if (!buildModeActive) return;

        HandleHeightAdjustment();
        HandlePreview();
        HandlePlacementInput();
        HandleRotationInput();
        HandleCycleInput();
        HandleDrag();
    }

    // ----------------------------------------------------------------
    // PREVIEW
    void HandlePreview()
    {
        if (previewObject == null)
        {
            if (buildPrefabs[selectedIndex]?.prefab == null) return;
            previewObject = Instantiate(buildPrefabs[selectedIndex].prefab);
            foreach (var c in previewObject.GetComponentsInChildren<Collider>())
                c.enabled = false;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 5000f, buildSurfaceMask))
        {
            Vector3 gridPos = grid.GetNearestPointOnGrid(hit.point);
            gridPos.y = currentHeight;

            // snap to grid corners (important!)
            gridPos.x = Mathf.Round(gridPos.x / grid.gridSize) * grid.gridSize;
            gridPos.z = Mathf.Round(gridPos.z / grid.gridSize) * grid.gridSize;

            previewObject.transform.position = gridPos + Vector3.up * 0.05f;
            previewObject.transform.rotation = rotation;
        }
    }

    // ----------------------------------------------------------------
    // SINGLE CLICK PLACEMENT
    void HandlePlacementInput()
    {
        if (isDragging) return;

        if (Input.GetMouseButtonDown(0))
        {
            var cat = buildPrefabs[selectedIndex].category;
            Vector3 pos = previewObject.transform.position;
            Quaternion rot = rotation;

            switch (cat)
            {
                case BuildCategory.Wall:
                case BuildCategory.Shopfront:
                    pos = grid.GetNearestPointOnGrid(pos);
                    pos.y = currentHeight;
                    Instantiate(buildPrefabs[selectedIndex].prefab, pos, rot);
                    break;

                case BuildCategory.Decor:
                case BuildCategory.Default:
                case BuildCategory.Runway:
                    Instantiate(buildPrefabs[selectedIndex].prefab, pos, rot);
                    break;
            }
        }
    }

    // ----------------------------------------------------------------
    // ROTATION
    void HandleRotationInput()
    {
        // Begin rotation
        if (Input.GetKeyDown(KeyCode.R) && previewObject != null)
        {
            rotating = true;
            lastMousePos = Input.mousePosition;
            lockedPreviewPos = previewObject.transform.position; // lock preview position
        }

        // While holding R, rotate according to horizontal mouse movement
        if (rotating && Input.GetKey(KeyCode.R))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            lastMousePos = Input.mousePosition;

            float rotationSpeed = Input.GetKey(KeyCode.LeftShift) ? 0.1f : 0.25f; // fine-tune option
            currentRotationY += delta.x * rotationSpeed;
            currentRotationY = Mathf.Repeat(currentRotationY, 360f);
            rotation = Quaternion.Euler(0, currentRotationY, 0);

            // Keep preview locked in place while rotating
            previewObject.transform.position = lockedPreviewPos;
            previewObject.transform.rotation = rotation;
        }

        // Release R to confirm rotation and unlock
        if (Input.GetKeyUp(KeyCode.R))
        {
            rotating = false;
        }
    }

    // ----------------------------------------------------------------
    // PREFAB CYCLING
    void HandleCycleInput()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Destroy(previewObject);
            selectedIndex = (selectedIndex + 1) % buildPrefabs.Length;
        }
    }

    // ----------------------------------------------------------------
    // HEIGHT / LAYER CONTROL (Sims-style)
    void HandleHeightAdjustment()
    {
        var cat = buildPrefabs[selectedIndex].category;

        if (cat == BuildCategory.Wall || cat == BuildCategory.Shopfront || cat == BuildCategory.Decor)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                currentLayer++;
                currentHeight = currentLayer * wallHeightStep;
                MoveCameraToHeight(currentHeight);
                GenerateFloorForLayer(currentLayer);
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                currentLayer = Mathf.Max(0, currentLayer - 1);
                currentHeight = currentLayer * wallHeightStep;
                MoveCameraToHeight(currentHeight);
            }
        }
        else currentHeight = 0f;
    }

    void MoveCameraToHeight(float height)
    {
        if (freeCam) freeCam.SetTargetHeight(height);
    }

    // ----------------------------------------------------------------
    // FLOOR GENERATION (Sims-style)
    void GenerateFloorForLayer(int layer)
    {
        string floorName = $"Floor_Layer_{layer}";
        if (GameObject.Find(floorName)) return;

        GameObject floorParent = new GameObject(floorName);
        floorParent.transform.position = new Vector3(0, layer * wallHeightStep, 0);

        // Define bounds by previously placed walls
        var placedAtLayer = placedObjects
            .Where(o => Mathf.Approximately(o.transform.position.y, layer * wallHeightStep))
            .ToList();

        if (placedAtLayer.Count == 0)
        {
            // create small default area 3x3 grid cells
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector3 pos = new Vector3(x * grid.gridSize, layer * wallHeightStep, z * grid.gridSize);
                    Instantiate(floorPrefab, pos, Quaternion.identity, floorParent.transform);
                }
            }
            return;
        }

        float minX = placedAtLayer.Min(o => o.transform.position.x);
        float maxX = placedAtLayer.Max(o => o.transform.position.x);
        float minZ = placedAtLayer.Min(o => o.transform.position.z);
        float maxZ = placedAtLayer.Max(o => o.transform.position.z);

        for (float x = minX; x <= maxX; x += grid.gridSize)
        {
            for (float z = minZ; z <= maxZ; z += grid.gridSize)
            {
                Vector3 pos = new Vector3(x, layer * wallHeightStep, z);
                Instantiate(floorPrefab, pos, Quaternion.identity, floorParent.transform);
            }
        }
    }

    // ----------------------------------------------------------------
    // DRAG BUILD (Walls, Shopfronts, Runways)
    void HandleDrag()
    {
        var cat = buildPrefabs[selectedIndex].category;
        if (cat != BuildCategory.Wall && cat != BuildCategory.Shopfront && cat != BuildCategory.Runway)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 5000f, buildSurfaceMask))
            return;

        Vector3 gridPos = grid.GetNearestPointOnGrid(hit.point);
        gridPos.y = currentHeight;

        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragStartPos = gridPos;
            dragEndPos = gridPos;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            dragEndPos = gridPos;
            UpdateDragPreview(dragStartPos, dragEndPos);
        }

        if (isDragging && Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            RemoveFirstPlacedPieceIfOverlapping(dragStartPos);
            PlaceDragPieces(dragStartPos, dragEndPos, cat);
            ClearDragPreview();
        }
    }

    void UpdateDragPreview(Vector3 start, Vector3 end)
    {
        ClearDragPreview();

        float segLen = GetSegmentLengthForCategory(buildPrefabs[selectedIndex].category);
        start = grid.GetNearestPointOnGrid(start);
        Vector3 dir = (end - start).normalized;

        // Snap to 45° increments
        dir.x = Mathf.Round(dir.x);
        dir.z = Mathf.Round(dir.z);
        dir.Normalize();

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        float distance = Vector3.Distance(start, end);
        int count = Mathf.Max(1, Mathf.CeilToInt(distance / segLen));
        Vector3 step = dir * segLen;

        for (int i = 0; i <= count; i++)
        {
            Vector3 pos = start + step * i;
            pos.y = currentHeight;
            GameObject ghost = Instantiate(buildPrefabs[selectedIndex].prefab, pos, rot);
            foreach (var col in ghost.GetComponentsInChildren<Collider>()) col.enabled = false;
            dragPreviewObjects.Add(ghost);
        }
    }

    void PlaceDragPieces(Vector3 start, Vector3 end, BuildCategory cat)
    {
        float segLen = GetSegmentLengthForCategory(cat);
        start = grid.GetNearestPointOnGrid(start);
        Vector3 dir = (end - start).normalized;
        dir.x = Mathf.Round(dir.x);
        dir.z = Mathf.Round(dir.z);
        dir.Normalize();

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        float distance = Vector3.Distance(start, end);
        int count = Mathf.Max(1, Mathf.CeilToInt(distance / segLen));
        Vector3 step = dir * segLen;

        for (int i = 0; i <= count; i++)
        {
            Vector3 pos = start + step * i;
            pos.y = currentHeight;

            // runway stretches only forward/backward
            if (cat == BuildCategory.Runway && Mathf.Abs(dir.x) > 0 && Mathf.Abs(dir.z) > 0)
                continue;

            GameObject piece = Instantiate(buildPrefabs[selectedIndex].prefab, pos, rot);
            placedObjects.Add(piece);
        }
    }

    void ClearDragPreview()
    {
        foreach (var o in dragPreviewObjects) Destroy(o);
        dragPreviewObjects.Clear();
    }

    float GetSegmentLengthForCategory(BuildCategory cat)
    {
        switch (cat)
        {
            case BuildCategory.Runway: return grid.gridSize * 3f;   // spans 3 tiles
            case BuildCategory.Shopfront: return grid.gridSize * 1.5f;
            case BuildCategory.Wall: return grid.gridSize;
            default: return grid.gridSize;
        }
    }

    void RemoveFirstPlacedPieceIfOverlapping(Vector3 startPos)
    {
        // Check for a wall placed very close to the drag start
        Collider[] hits = Physics.OverlapSphere(startPos, grid.gridSize * 0.4f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Wall")) // or use your prefab category check
            {
                Destroy(hit.gameObject);
                break;
            }
        }
    }
}