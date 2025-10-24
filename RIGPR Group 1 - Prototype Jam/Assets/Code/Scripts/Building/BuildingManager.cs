using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    // CORE REFERENCES
    // Handles global scene objects and key build dependencies
    [Header("Core References")]
    public Camera mainCamera;            // Camera used for raycasting placement
    public GridManager grid;             // Reference to grid snapping system
    public FreeCamera freeCam;           // Player camera movement script
    public LayerMask buildSurfaceMask;   // Defines surfaces that can be built on

    // ENUMS AND PREFAB TYPES
    // Defines categories for build behavior & rotation handling
    public enum BuildCategory { Default, Wall, Shopfront, Decor, Runway }

    [System.Serializable]
    public class BuildPrefab
    {
        public GameObject prefab;        // Prefab reference to instantiate
        public BuildCategory category;   // Determines special placement rules
    }

    // ROTATION STATE
    // Controls both 45 snapping (tap R) and fine rotation (hold R)
    private bool rotating = false;
    private float currentRotationY = 0f; // Y-axis rotation accumulator
    private Vector3 lastMousePos;        // Tracks mouse delta for fine rotation
    private Vector3 lockedPreviewPos;    // Keeps preview in place while rotating

    // PREFABS AND STATE MANAGEMENT
    // Tracks which prefab is active and which objects have been built
    [Header("Prefabs & State")]
    public BuildPrefab[] buildPrefabs;   // All buildable prefabs
    private int selectedIndex = 0;       // Current prefab index
    private GameObject previewObject;    // The live “ghost” preview object
    private Quaternion rotation = Quaternion.identity;
    private List<GameObject> placedObjects = new List<GameObject>();

    // HEIGHT / LAYERING
    // Handles Sims-style floor layering system
    [Header("Height / Layering")]
    public float wallHeightStep = 20f;   // Vertical offset between layers
    private float currentHeight = 0f;    // Current layer Y-height
    private int currentLayer = 0;        // Active layer index

    // FLOOR GENERATION
    // Generates simple floors above placed walls
    [Header("Floor Generation")]
    public GameObject floorPrefab;       // Prefab for floor tile generation


    // BUILD MODE SETTINGS
    // Controls enabling/disabling the build system and UI visibility

    [Header("Build Mode Settings")]
    public bool buildModeActive = false;
    public GameObject buildUI;

    // DRAG BUILD SYSTEM (Walls, Shopfronts, Runways)
    // Tracks mouse dragging and preview line generation
    [Header("Drag Build Settings")]
    private bool isDragging = false;
    private Vector3 dragStartPos;
    private Vector3 dragEndPos;
    private List<GameObject> dragPreviewObjects = new List<GameObject>();

    void Update()
    {
        // Toggle Build Mode with B
        if (Input.GetKeyDown(KeyCode.B))
        {
            buildModeActive = !buildModeActive;
            if (buildUI) buildUI.SetActive(buildModeActive);
            if (!buildModeActive && previewObject) Destroy(previewObject);
        }

        if (!buildModeActive) return;

        HandleHeightAdjustment();  // E/Q to change layer
        HandlePreview();           // Show placement preview
        HandlePlacementInput();    // Single-tile placement
        HandleRotationInput();     // R rotation controls
        HandleCycleInput();        // Tab to cycle prefabs
        HandleDrag();              // Click+drag placement
    }

    // PREVIEW HANDLING
    // Creates a transparent preview object that follows the grid
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
            Vector3 pos = hit.point;
            pos.y = currentHeight;

            var cat = buildPrefabs[selectedIndex].category;

            // --- NEW: Free placement mode for Decor/Default while holding Shift ---
            bool freePlacement = (Input.GetKey(KeyCode.LeftShift) &&
                                  (cat == BuildCategory.Decor || cat == BuildCategory.Default));

            if (!freePlacement)
            {
                pos = grid.GetNearestPointOnGrid(pos);
                pos.x = Mathf.Round(pos.x / grid.gridSize) * grid.gridSize;
                pos.z = Mathf.Round(pos.z / grid.gridSize) * grid.gridSize;
            }

            previewObject.transform.position = pos + Vector3.up * 0.05f;
            previewObject.transform.rotation = rotation;
        }
    }

    // SINGLE CLICK PLACEMENT
    // Used for Decor, Towers, Hangars (non-drag types)
    void HandlePlacementInput()
    {
        if (isDragging) return;

        if (Input.GetMouseButtonDown(0))
        {
            var cat = buildPrefabs[selectedIndex].category;
            Vector3 pos = previewObject.transform.position;
            Quaternion rot = rotation;

            bool freePlacement = (Input.GetKey(KeyCode.LeftShift) &&
                                  (cat == BuildCategory.Decor || cat == BuildCategory.Default));

            if (!freePlacement)
                pos = grid.GetNearestPointOnGrid(pos);

            pos.y = currentHeight;

            GameObject piece = Instantiate(buildPrefabs[selectedIndex].prefab, pos, rot);
            placedObjects.Add(piece);
        }
    }

    // ROTATION INPUT
    // Tap R = 45 snap; Hold R = fine rotation (for Decor/Default)
    void HandleRotationInput()
    {
        if (previewObject == null) return;

        BuildCategory cat = buildPrefabs[selectedIndex].category;
        bool freeRotate = (cat == BuildCategory.Decor || cat == BuildCategory.Default);

        // Tap R to rotate by 45 degrees (universal)
        if (Input.GetKeyDown(KeyCode.R))
        {
            currentRotationY = Mathf.Repeat(currentRotationY + 45f, 360f);
            rotation = Quaternion.Euler(0f, currentRotationY, 0f);
            previewObject.transform.rotation = rotation;

            // Enable fine rotation mode for decor/default
            if (freeRotate)
            {
                rotating = true;
                lastMousePos = Input.mousePosition;
                lockedPreviewPos = previewObject.transform.position;
            }
        }

        // Hold R for continuous rotation (decor/default only)
        if (rotating && Input.GetKey(KeyCode.R) && freeRotate)
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            lastMousePos = Input.mousePosition;

            float speed = Input.GetKey(KeyCode.LeftShift) ? 0.15f : 0.35f;
            currentRotationY = Mathf.Repeat(currentRotationY + delta.x * speed, 360f);
            rotation = Quaternion.Euler(0f, currentRotationY, 0f);

            // Keep preview locked while rotating
            previewObject.transform.position = lockedPreviewPos;
            previewObject.transform.rotation = rotation;
        }

        // Release R to end fine rotation mode
        if (Input.GetKeyUp(KeyCode.R))
            rotating = false;
    }

    // PREFAB CYCLING (TAB)
    void HandleCycleInput()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            Destroy(previewObject);
            selectedIndex = (selectedIndex + 1) % buildPrefabs.Length;
        }
    }

    // LAYER & HEIGHT MANAGEMENT
    // Sims-style system for multi-storey builds
    void HandleHeightAdjustment()
    {
        var cat = buildPrefabs[selectedIndex].category;

        if (cat == BuildCategory.Wall || cat == BuildCategory.Shopfront || cat == BuildCategory.Decor)
        {
            // Go up a layer
            if (Input.GetKeyDown(KeyCode.E))
            {
                currentLayer++;
                currentHeight = currentLayer * wallHeightStep;
                MoveCameraToHeight(currentHeight);

                // Always rebuild floor for this layer (so new walls below count)
                GenerateFloorForLayer(currentLayer);

                // Refresh visibility after floor generation
                ApplyLayerVisibility(currentLayer);
            }

            // Go down a layer
            if (Input.GetKeyDown(KeyCode.Q))
            {
                currentLayer = Mathf.Max(0, currentLayer - 1);
                currentHeight = currentLayer * wallHeightStep;
                MoveCameraToHeight(currentHeight);

                // Refresh visibility so upper layers hide again
                ApplyLayerVisibility(currentLayer);
            }
        }
        else
        {
            currentHeight = 0f;
        }
    }

    void MoveCameraToHeight(float height)
    {
        if (freeCam) freeCam.SetTargetHeight(height);
    }

    // FLOOR GENERATION (Sims-style)
    // Builds floor tiles above walls or closed rooms
    void GenerateFloorForLayer(int layer)
    {
        // Name + rebuild if floor already exists
        string floorName = $"Floor_Layer_{layer}";
        var old = GameObject.Find(floorName);
        if (old != null) Destroy(old); // Rebuild every time

        // Parent object to keep hierarchy tidy
        GameObject floorParent = new GameObject(floorName);
        floorParent.transform.position = new Vector3(0, layer * wallHeightStep, 0);

        // Collect all walls on the layer BELOW
        var wallObjs = placedObjects
            .Where(o => o != null &&
                (o.CompareTag("Wall")) &&
                Mathf.Approximately(o.transform.position.y, (layer - 1) * wallHeightStep))
            .ToList();

        if (wallObjs.Count == 0) return; // no walls = no floor

        // Group nearby walls into "rooms"
        List<List<GameObject>> roomGroups = GroupWallsByProximity(wallObjs, grid.gridSize * 2f);

        // Fill the area between walls with floor tiles
        foreach (var group in roomGroups)
        {
            if (group.Count < 3) continue;

            bool isClosed = IsRoomClosed(group, grid.gridSize * 1.5f);

            // --- Accurate bounding box based on rotated wall bounds ---
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var wall in group)
            {
                if (wall == null) continue;
                Renderer rend = wall.GetComponentInChildren<Renderer>();
                if (rend == null) continue;

                Bounds b = rend.bounds; // world-space bounds respect rotation
                minX = Mathf.Min(minX, b.min.x);
                maxX = Mathf.Max(maxX, b.max.x);
                minZ = Mathf.Min(minZ, b.min.z);
                maxZ = Mathf.Max(maxZ, b.max.z);
            }

            // --- Slight inner offset to prevent visible lip ---
            float padding = grid.gridSize * 0.05f;
            minX += padding;
            maxX -= padding;
            minZ += padding;
            maxZ -= padding;

            // --- Fill the bounded area with floor tiles ---
            for (float x = minX; x <= maxX; x += grid.gridSize)
            {
                for (float z = minZ; z <= maxZ; z += grid.gridSize)
                {
                    Vector3 tilePos = new Vector3(x, layer * wallHeightStep, z);

                    if (isClosed)
                    {
                        // Closed room - fill fully
                        Instantiate(floorPrefab, tilePos, Quaternion.identity, floorParent.transform);
                    }
                    else
                    {
                        // Open layout - only place tiles near walls
                        bool nearWall = group.Any(w =>
                            Vector3.Distance(tilePos, w.transform.position) <= grid.gridSize * 1.1f);
                        if (nearWall)
                            Instantiate(floorPrefab, tilePos, Quaternion.identity, floorParent.transform);
                    }
                }
            }
        }

        // After generating floors, refresh visibility
        ApplyLayerVisibility(layer);
    }

    void ApplyLayerVisibility(int activeLayer)
    {
        float currentY = activeLayer * wallHeightStep;

        // --- Handle placed objects ---
        foreach (var obj in placedObjects)
        {
            if (obj == null) continue;

            float objY = obj.transform.position.y;
            bool isWall = obj.CompareTag("Wall") || obj.CompareTag("Shopfront");

            // Walls: visible if at or below current layer
            if (isWall)
            {
                obj.SetActive(objY <= currentY);
            }
            else
            {
                // Non-walls: visible only on the current layer
                obj.SetActive(Mathf.Approximately(objY, currentY));
            }
        }

        // --- Handle floors ---
        foreach (Transform child in transform)
        {
            if (!child.name.StartsWith("Floor_Layer_")) continue;

            float layerY = child.position.y;
            int floorLayerIndex = Mathf.RoundToInt(layerY / wallHeightStep);

            // Show all floors below or equal to current layer
            bool visible = floorLayerIndex <= activeLayer;
            child.gameObject.SetActive(visible);
        }

        // --- Extra safety for tagged floor objects (optional) ---
        var allFloors = GameObject.FindGameObjectsWithTag("Floor");
        foreach (var floor in allFloors)
        {
            int floorLayerIndex = Mathf.RoundToInt(floor.transform.position.y / wallHeightStep);
            floor.SetActive(floorLayerIndex <= activeLayer);
        }
    }

    bool IsRoomClosed(List<GameObject> walls, float threshold)
    {
        if (walls.Count < 3) return false;

        foreach (var wall in walls)
        {
            bool hasNeighbor = walls.Any(other =>
                other != wall &&
                Vector3.Distance(wall.transform.position, other.transform.position) <= threshold);
            if (!hasNeighbor) return false;
        }
        return true;
    }

    List<List<GameObject>> GroupWallsByProximity(List<GameObject> walls, float threshold)
    {
        List<List<GameObject>> groups = new List<List<GameObject>>();
        HashSet<GameObject> visited = new HashSet<GameObject>();

        foreach (var wall in walls)
        {
            if (visited.Contains(wall)) continue;

            List<GameObject> group = new List<GameObject>();
            Queue<GameObject> toVisit = new Queue<GameObject>();
            toVisit.Enqueue(wall);

            while (toVisit.Count > 0)
            {
                var current = toVisit.Dequeue();
                if (visited.Contains(current)) continue;
                visited.Add(current);
                group.Add(current);

                foreach (var other in walls)
                {
                    if (other == current || visited.Contains(other)) continue;
                    if (Vector3.Distance(current.transform.position, other.transform.position) <= threshold)
                        toVisit.Enqueue(other);
                }
            }

            if (group.Count > 0)
                groups.Add(group);
        }

        return groups;
    }
    // DRAG PLACEMENT SYSTEM
    // Used for continuous wall, shopfront, and runway placement
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

        // Start Drag
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragStartPos = gridPos;
            dragEndPos = gridPos;
        }

        // While Dragging
        if (isDragging && Input.GetMouseButton(0))
        {
            Vector3 localDir = (gridPos - dragStartPos);

            // Constrain to preview’s forward (local Z) axis
            Vector3 forward = rotation * Vector3.forward;  // local Z of current rotation
            float projection = Vector3.Dot(localDir, forward.normalized);
            dragEndPos = dragStartPos + forward.normalized * projection;

            UpdateDragPreview(dragStartPos, dragEndPos);
        }

        // End Drag
        if (isDragging && Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            RemoveFirstPlacedPieceIfOverlapping(dragStartPos);
            PlaceDragPieces(dragStartPos, dragEndPos, cat);
            ClearDragPreview();
        }
    }

    // DRAG PREVIEW
    // Visualizes continuous placement while dragging
    void UpdateDragPreview(Vector3 start, Vector3 end)
    {
        ClearDragPreview();

        float segLen = GetSegmentLengthForCategory(buildPrefabs[selectedIndex].category);
        start = grid.GetNearestPointOnGrid(start);
        Vector3 dir = (end - start).normalized;

        // Snap to 45 degree increments
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

    // FINALIZE DRAG PLACEMENT
    void PlaceDragPieces(Vector3 start, Vector3 end, BuildCategory cat)
    {
        float segLen = GetSegmentLengthForCategory(cat);
        start = grid.GetNearestPointOnGrid(start);
        Vector3 dir = (end - start).normalized;
        dir.x = Mathf.Round(dir.x);
        dir.z = Mathf.Round(dir.z);
        dir.Normalize();

        Quaternion rot = rotation; // Use preview rotation

        float distance = Vector3.Distance(start, end);
        int count = Mathf.Max(1, Mathf.CeilToInt(distance / segLen));
        Vector3 step = dir * segLen;

        for (int i = 0; i <= count; i++)
        {
            Vector3 pos = start + step * i;
            pos.y = currentHeight;

            if (cat == BuildCategory.Runway && Mathf.Abs(dir.x) > 0 && Mathf.Abs(dir.z) > 0)
                continue;

            GameObject piece = Instantiate(buildPrefabs[selectedIndex].prefab, pos, rot);
            placedObjects.Add(piece);
        }
    }

    // PREVIEW CLEANUP
    void ClearDragPreview()
    {
        foreach (var o in dragPreviewObjects) Destroy(o);
        dragPreviewObjects.Clear();
    }

    // SEGMENT LENGTH BY CATEGORY
    // Defines how far apart wall segments are
    float GetSegmentLengthForCategory(BuildCategory cat)
    {
        switch (cat)
        {
            case BuildCategory.Runway: return grid.gridSize * 3f;
            case BuildCategory.Shopfront: return grid.gridSize * 1.5f;
            case BuildCategory.Wall: return grid.gridSize;
            default: return grid.gridSize;
        }
    }

    // PREVENT DOUBLE PLACEMENT
    // Deletes overlapping wall from initial click
    void RemoveFirstPlacedPieceIfOverlapping(Vector3 startPos)
    {
        Collider[] hits = Physics.OverlapSphere(startPos, grid.gridSize * 0.4f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Wall"))
            {
                Destroy(hit.gameObject);
                break;
            }
        }
    }
}
