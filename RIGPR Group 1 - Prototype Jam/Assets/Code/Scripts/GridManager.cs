using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public float gridSize = 20f;          // Size of one grid cell (spacing)
    public Color gridColor = Color.gray;  // Color used for visual gizmos in scene view
    public int gridExtent = 500;           // How far the grid extends from origin in gizmos

    // GRID SNAP LOGIC
    // Returns nearest snapped point on the grid given a world-space position.
    public Vector3 GetNearestPointOnGrid(Vector3 position)
    {
        // Snap each axis relative to grid origin (this GameObject)
        Vector3 localPos = position - transform.position;

        float xCount = Mathf.Round(localPos.x / gridSize);
        float yCount = Mathf.Round(localPos.y / gridSize);
        float zCount = Mathf.Round(localPos.z / gridSize);

        Vector3 result = new Vector3(
            xCount * gridSize,
            yCount * gridSize,
            zCount * gridSize
        );

        // Return back to world space
        return result + transform.position;
    }


    // GRID VISUALIZATION
    // Draws visible grid lines in the Scene view (editor only).
    private void OnDrawGizmos()
    {
        Gizmos.color = gridColor;

        // Draw grid lines centered on the object’s transform
        for (float x = -gridExtent; x <= gridExtent; x += gridSize)
        {
            for (float z = -gridExtent; z <= gridExtent; z += gridSize)
            {
                Vector3 start = GetNearestPointOnGrid(new Vector3(x, 0, -gridExtent)) + transform.position;
                Vector3 end = GetNearestPointOnGrid(new Vector3(x, 0, gridExtent)) + transform.position;
                Gizmos.DrawLine(start, end);

                start = GetNearestPointOnGrid(new Vector3(-gridExtent, 0, z)) + transform.position;
                end = GetNearestPointOnGrid(new Vector3(gridExtent, 0, z)) + transform.position;
                Gizmos.DrawLine(start, end);
            }
        }

        // Draw a small origin marker at (0,0,0)
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.5f);
    }
}