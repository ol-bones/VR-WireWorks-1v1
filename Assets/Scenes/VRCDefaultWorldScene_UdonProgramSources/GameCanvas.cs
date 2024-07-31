
using System;
using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class GameCanvas : UdonSharpBehaviour
{
    public int m_SeatId = -1;
    public GameObject linePrefab; // Prefab with MeshFilter and MeshRenderer
    public float lineWidth = 0.1f;
    public int maxLines = 100; // Adjust as needed

    private Mesh lineMesh;
    private Vector3[] vertices;
    private int[] indices;
    private Color[] colors;
    private int vertexIndex;

    public Vector2[] m_Grid;
    public int m_GridSize;

    public GameObject m_DotPrefab;
    public Dot[] m_Dots;

    public Dot m_SelectedDot_A;
    public Dot m_SelectedDot_B;
    public GameObject m_LinkPrefab; // Prefab with Link component

    public GameObject m_BoxPrefab;

    void Start()
    {
        Game game = this.GetGame();
        if (game == null) { Debug.LogError("GameCanvas.cs Start: game == null"); return; }

        this.m_GridSize = game.m_GridSize;
        
        // Check if the linePrefab is assigned
        if (linePrefab == null)
        {
            Debug.LogError("LineMeshDrawer: linePrefab is not assigned.");
            return;
        }

        // Instantiate the line prefab
        GameObject lineObject = UnityEngine.Object.Instantiate(linePrefab);
        if (lineObject == null)
        {
            Debug.LogError("LineMeshDrawer: Failed to instantiate linePrefab.");
            return;
        }
        lineObject.transform.SetParent(transform, false);

        // Initialize the mesh and its components
        MeshFilter meshFilter = lineObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("LineMeshDrawer: MeshFilter component not found on linePrefab.");
            return;
        }

        MeshRenderer meshRenderer = lineObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogError("LineMeshDrawer: MeshRenderer component not found on linePrefab.");
            return;
        }

        lineMesh = new Mesh();
        meshFilter.mesh = lineMesh;

        // Preallocate arrays for vertices, indices, and colors
        int maxVertices = maxLines * 2;
        vertices = new Vector3[maxVertices];
        indices = new int[maxVertices];
        colors = new Color[maxVertices];
        vertexIndex = 0;

        //GenerateGrid();
        //DrawGrid();
        GenerateDotGrid();

        Debug.Log("GameCanvas initialized.");
    }

    public void GenerateDotGrid()
    {
        if (this.m_DotPrefab == null)
        {
            Debug.LogError("Dot prefab is null");
            return;
        }

        this.m_Dots = new Dot[m_GridSize * m_GridSize];

        float circleRadius = (1f / m_GridSize) / 2.0f;
        for (int y = 0; y < m_GridSize; y++)
        {
            for (int x = 0; x < m_GridSize; x++)
            {
                GameObject dot = UnityEngine.Object.Instantiate(this.m_DotPrefab);
                if (dot == null)
                {
                    Debug.LogError("Failed to instantiate dot prefab");
                    return;
                }

                Debug.Log($"Dot instantiated at position ({x}, {y})");

                MeshFilter meshFilter = dot.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    Debug.LogError("MeshFilter component not found on dot prefab");
                    Destroy(dot);
                    return;
                }
                Debug.Log("MeshFilter component found");

                MeshRenderer meshRenderer = dot.GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    Debug.LogError("MeshRenderer component not found on dot prefab");
                    Destroy(dot);
                    return;
                }
                Debug.Log("MeshRenderer component found");

                CapsuleCollider collider = dot.GetComponent<CapsuleCollider>();
                if (collider == null)
                {
                    Debug.LogError("CapsuleCollider component not found on dot prefab");
                    Destroy(dot);
                    return;
                }
                Debug.Log("CapsuleCollider component found");

                Vector3 originalScale = dot.transform.localScale;
                Quaternion originalRotation = this.transform.rotation;
                this.transform.rotation = Quaternion.identity;

                dot.transform.SetParent(this.transform);
                dot.transform.localScale = new Vector3(circleRadius/2.0f, 0.005f, circleRadius/2.0f);

                this.transform.rotation = originalRotation;
                Vector3 dotPosition;
                if (this.transform.forward.y < 0)
                {
                    dotPosition = new Vector3(
                        -0.5f + circleRadius + (2 * circleRadius * x),
                        -0.5f + circleRadius + (2 * circleRadius * y),
                        0
                    );
                }
                else
                {
                    dotPosition = new Vector3(
                        -0.5f + circleRadius + (2 * circleRadius * (m_GridSize - 1 - x)), // Reverse the x coordinate
                        -0.5f + circleRadius + (2 * circleRadius * y),
                        0
                    );
                }

                dot.transform.localPosition = dotPosition;


                dot.SetActive(true);
                //this.InteractionText = $"Dot {x}, {y}";
                UdonBehaviour udonBehaviour = dot.GetComponent<UdonBehaviour>();
                //udonBehaviour.interactText = this.InteractionText;
                //udonBehaviour.InteractionText = this.InteractionText;

                Dot dotComponent = dot.GetComponent<Dot>();
                if (dotComponent == null)
                {
                    Debug.LogError("Dot component is null");
                    Destroy(dot);
                    return;
                }
                
                dotComponent.m_GridPosition = new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
                dotComponent.m_GridSize = m_GridSize;

                Debug.Log("Dot component found");

                this.m_Dots[y * m_GridSize + x] = dotComponent;

                dotComponent.Hide();
                Debug.Log($"Dot placed in array at index {y * m_GridSize + x}");
            }
        }

        Debug.Log("Dot grid generation complete");
    }

    public void ShowAllDots()
    {
        foreach (Dot dot in this.m_Dots)
        {
            dot.Show();
        }
    }

    public bool RemainingDotsUnlinkable()
    {
        foreach (Dot dot in m_Dots)
        {
            if (dot.IsVisible()) // Dot is not fully linked
            {
                Dot[] adjacentDots = GetAdjacentDots(dot);
                foreach (Dot adjacentDot in adjacentDots)
                {
                    if (adjacentDot != null && adjacentDot.IsVisible()) // Adjacent dot is not fully linked
                    {
                        return false; // Found at least one pair of dots that can be linked
                    }
                }
            }
        }

        return true; // No dots can be linked
    }

    public void UpdateSeatId(int seatid)
    {
        this.m_SeatId = seatid;
        for (int i = 0; i < m_Dots.Length; i++)
        {
            m_Dots[i].m_SeatId = seatid;
        }
    }

    public Dot GetDotAtPosition(Vector2Int position)
    {
        if (position.x < 0 || position.x >= m_GridSize || position.y < 0 || position.y >= m_GridSize)
        {
            return null;
        }

        return this.m_Dots[(int)position.y * m_GridSize + (int)position.x];
    }

    public Dot[] GetAdjacentDots(Dot dot)
    {
        Dot[] adjacentDots = new Dot[4];
        adjacentDots[0] = dot.GetDotUp();
        adjacentDots[1] = dot.GetDotRight();
        adjacentDots[2] = dot.GetDotDown();
        adjacentDots[3] = dot.GetDotLeft();

        return adjacentDots;
    }

    public bool DotsAreAdjacent(Dot dotA, Dot dotB)
    {
        Dot[] adjacentDots = this.GetAdjacentDots(dotA);

        foreach (Dot adjacentDot in adjacentDots)
        {
            if (adjacentDot == dotB)
            {
                return true;
            }
        }

        return false;
    }


    // Returns 2 if this made a box otherwise 1 if link without square otherwise 0 if not end of turn
    public int On_DotSelected(Game game, Dot dot, Color color)
    {
        Debug.Log("Dot selected");

        if (this.m_SelectedDot_A == null)
        {
            if (dot.IsFullyLinked())
            {
                Debug.Log("Dot is fully linked");
                return 0;
            }
            this.m_SelectedDot_A = dot;

            Debug.Log("Dot A selected");
        }
        else if (this.m_SelectedDot_B == null)
        {
            this.m_SelectedDot_B = dot;

            if(this.m_SelectedDot_A == this.m_SelectedDot_B)
            {
                Debug.Log("Same dot selected");
                this.m_SelectedDot_A.m_Selected = false;
                this.m_SelectedDot_A = null;
                this.m_SelectedDot_B = null;
                return -1;
            } else if (this.m_SelectedDot_B.IsFullyLinked())
            {
                Debug.Log("Dot is fully linked");
                this.m_SelectedDot_A.m_Selected = false;
                this.m_SelectedDot_B.m_Selected = false;
                this.m_SelectedDot_A = null;
                this.m_SelectedDot_B = null;
                return -1;
            }

            if(this.DotsAreAdjacent(this.m_SelectedDot_A, this.m_SelectedDot_B))
            {
                Debug.Log("Dot B selected");

                // DrawLine(
                //     new Vector3(
                //         (2 * this.m_SelectedDot_A.transform.localPosition.x),
                //         (2 * this.m_SelectedDot_A.transform.localPosition.y),
                //         0
                //     ),
                //     new Vector3(
                //         (2 * this.m_SelectedDot_B.transform.localPosition.x),
                //         (2 * this.m_SelectedDot_B.transform.localPosition.y),
                //         0
                //     ),
                //     color
                // );

                // check if already linked
                if (this.m_SelectedDot_A.IsLinkedTo(this.m_SelectedDot_B))
                {
                    Debug.Log("Dots are already linked");
                    this.m_SelectedDot_A.m_Selected = false;
                    this.m_SelectedDot_B.m_Selected = false;
                    this.m_SelectedDot_A = null;
                    this.m_SelectedDot_B = null;
                    return -1;
                }


                int result = this.m_SelectedDot_A.LinkTo(this.m_SelectedDot_B, color);
                if(this.m_SelectedDot_A.IsFullyLinked())
                {
                    this.m_SelectedDot_A.Hide();
                }

                if(this.m_SelectedDot_B.IsFullyLinked())
                {
                    this.m_SelectedDot_B.Hide();
                }

                this.m_SelectedDot_A.m_Selected = false;
                this.m_SelectedDot_B.m_Selected = false;
                this.m_SelectedDot_A = null;
                this.m_SelectedDot_B = null;

                return result;
            }
            else
            {
                Debug.Log("Dots are not adjacent");
                this.m_SelectedDot_A.m_Selected = false;
                this.m_SelectedDot_B.m_Selected = false;
                this.m_SelectedDot_A = null;
                this.m_SelectedDot_B = null;
                return -1;
            }
        }

        return 0;
    }

    public void GenerateGrid()
    {
        this.m_Grid = new Vector2[m_GridSize * m_GridSize];

        for (int i = 0; i < m_GridSize; i++)
        {
            for (int j = 0; j < m_GridSize; j++)
            {
                m_Grid[i * m_GridSize + j] = new Vector2(i, j);
            }
        }
    }

    public void DrawGrid()
    {
        float circleRadius = 1f / m_GridSize;
        for (int i = 0; i < m_GridSize; i++)
        {
            for (int j = 0; j < m_GridSize; j++)
            {
                DrawCircle(
                    new Vector3(-1 + circleRadius + (2 * circleRadius * j), -1 + circleRadius + (2 * circleRadius * i), 0),
                    circleRadius / 2.0f,
                    20,
                    Color.red
                );
            }
        }
    }


    public void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        Vector3 scale = this.gameObject.transform.localScale * 0.5f;

        start = Vector3.Scale(start, scale);
        end = Vector3.Scale(end, scale);
        // Set z component to a fixed value
        start.z = 0;
        end.z = 0;

        if (vertexIndex >= vertices.Length)
        {
            Debug.LogError("Maximum number of lines reached. Cannot draw more lines.");
            return;
        }

        vertices[vertexIndex] = start;
        vertices[vertexIndex + 1] = end;
        indices[vertexIndex] = vertexIndex;
        indices[vertexIndex + 1] = vertexIndex + 1;
        colors[vertexIndex] = color;
        colors[vertexIndex + 1] = color;

        vertexIndex += 2;

        UpdateMesh();
    }

    public void DrawCircle(Vector3 center, float radius, int segments, Color color)
    {
        for (int i = 0; i < segments; i++)
        {
            float angle1 = Mathf.PI * 2 * i / segments;
            float angle2 = Mathf.PI * 2 * (i + 1) / segments;

            Vector3 start = new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, 0);
            Vector3 end = new Vector3(Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius, 0);

            DrawLine(center + start, center + end, color);
        }
    }

    private void UpdateMesh()
    {
        lineMesh.Clear();
        lineMesh.vertices = vertices;
        lineMesh.SetIndices(indices, MeshTopology.Lines, 0);
        lineMesh.colors = colors;
    }

    public Game GetGame()
    {
        GameObject gameCanvasObject = this.gameObject;
        Transform gameInterfaceTransform = gameCanvasObject.transform.parent;
        if (gameInterfaceTransform == null)
        {
            Debug.LogError("GameInterfaceTransform not found");
            return null;
        }

        GameObject gameInterfaceObject = gameInterfaceTransform.gameObject;
        Transform gameTransform = gameInterfaceObject.transform.parent;
        if (gameTransform == null)
        {
            Debug.LogError("GameTransform not found");
            return null;
        }

        GameObject gameObject = gameTransform.gameObject;
        //Debug.Log("Found Game GameObject: " + gameObject.name);
        Game game = gameObject.GetComponent<Game>();
        if (game == null)
        {
            Debug.LogError("Game component not found on Game GameObject");
            return null;
        }

        return game;
    }

    public void UnhighlightAllDots()
    {
        foreach (Dot dot in this.m_Dots)
        {
            dot.Unhighlight();
        }
    }

    public void ClearLines()
    {
        vertexIndex = 0;

        lineMesh.Clear();
        vertices = new Vector3[maxLines * 2];
        indices = new int[maxLines * 2];
        colors = new Color[maxLines * 2];
        vertexIndex = 0;

        UpdateMesh();
    }

    public void ResetSelections()
    {
        if (this.m_SelectedDot_A != null)
        {
            this.m_SelectedDot_A.m_Selected = false;
            this.m_SelectedDot_A = null;
        }

        if (this.m_SelectedDot_B != null)
        {
            this.m_SelectedDot_B.m_Selected = false;
            this.m_SelectedDot_B = null;
        }
    }

    public void ResetEverything()
    {
        foreach (Dot dot in this.m_Dots)
        {
            dot.Reset();
        }
    }
}
