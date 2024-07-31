
using UdonSharp;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using VRC.SDKBase;
using VRC.Udon;

public class Link : UdonSharpBehaviour
{
    public Vector3 m_StartPosition;
    public Vector3 m_EndPosition;

    public Dot m_ConnectedDot_A;
    public Dot m_ConnectedDot_B;

    public Vector2 m_LinkDirection;

    public Box m_LinkedBoxA;
    public Box m_LinkedBoxB;

    public bool m_IsBeingDeleted = false;
    void Start()
    {
    }

    public static bool InstantiateLink(GameCanvas gameCanvas, GameObject linkPrefab, Dot dotA, Dot dotB, Color color, GameObject boxPrefab)
    {
        // Instantiate the link prefab
        GameObject linkObject = UnityEngine.Object.Instantiate(linkPrefab);
        if (linkObject == null)
        {
            Debug.LogError("Link: Failed to instantiate linkPrefab.");
            return false;
        }

        // Get the Link component
        Link link = linkObject.GetComponent<Link>();
        if (link == null)
        {
            Debug.LogError("Link component not found on linkPrefab.");
            return false;
        }

        // Set the color of the link
        link.GetComponent<Renderer>().material.color = color;

        link.m_ConnectedDot_A = dotA;
        link.m_ConnectedDot_B = dotB;

        // Set the position and orientation of the Link object
        link.SetPositionAndOrientation();
        link.SetDotLinks(link);

        link.CheckForSquares(gameCanvas, out Link[] squareA, out Link[] squareB);
        bool formedSquare = false;
        if(squareA != null && link.IsValidSquare(squareA))
        {
            formedSquare = Box.InstantiateBox(gameCanvas, squareA, boxPrefab, Color.red, link);
        }
        if(squareB != null && link.IsValidSquare(squareB))
        {
            formedSquare = Box.InstantiateBox(gameCanvas, squareB, boxPrefab, Color.blue, link);
        }

        return formedSquare;
    }

    public void LinkBox(Box box)
    {
        if (this.m_LinkedBoxA == null)
        {
            this.m_LinkedBoxA = box;
        }
        else if (this.m_LinkedBoxB == null)
        {
            this.m_LinkedBoxB = box;
        }
        else
        {
            Debug.LogError("Link: LinkBox: Both boxes are already linked.");
        }
    }

    public void SetDotLinks(Link link)
    {
        if (m_ConnectedDot_A == null || m_ConnectedDot_B == null)
        {
            return;
        }

        if (link.m_LinkDirection == Vector2Int.up)
        {
            link.m_ConnectedDot_A.m_LinkUp = link;
            link.m_ConnectedDot_B.m_LinkDown = link;
        }
        else if (link.m_LinkDirection == Vector2Int.down)
        {
            link.m_ConnectedDot_A.m_LinkDown = link;
            link.m_ConnectedDot_B.m_LinkUp = link;
        }
        else if (link.m_LinkDirection == Vector2Int.left)
        {
            link.m_ConnectedDot_A.m_LinkLeft = link;
            link.m_ConnectedDot_B.m_LinkRight = link;
        }
        else if (link.m_LinkDirection == Vector2Int.right)
        {
            link.m_ConnectedDot_A.m_LinkRight = link;
            link.m_ConnectedDot_B.m_LinkLeft = link;
        }
    }

    public void SetPositionAndOrientation()
    {
        this.transform.SetParent(m_ConnectedDot_A.transform);

        Vector3 direction = (m_ConnectedDot_B.transform.position - m_ConnectedDot_A.transform.position).normalized;
        float dotARadius = m_ConnectedDot_A.transform.localScale.x / 2;
        float dotBRadius = m_ConnectedDot_B.transform.localScale.x;

        this.m_StartPosition = m_ConnectedDot_A.transform.position;
        this.m_EndPosition = m_ConnectedDot_B.transform.position;

        this.transform.position = (this.m_StartPosition + this.m_EndPosition) / 2;
        this.transform.LookAt(this.m_EndPosition);

        Vector3 scale = this.transform.localScale;
        scale.z = Vector3.Distance(this.m_StartPosition, this.m_EndPosition) * 2.0f;
        scale.x = 0.01f;
        scale.y = 0.01f;

        this.transform.localScale = scale;
        
        this.m_LinkDirection = new Vector2(
            this.m_ConnectedDot_B.m_GridPosition.x - this.m_ConnectedDot_A.m_GridPosition.x,
            this.m_ConnectedDot_B.m_GridPosition.y - this.m_ConnectedDot_A.m_GridPosition.y
        );
    }

    public Dot GetOtherDot(Dot dot)
    {
        if (dot == m_ConnectedDot_A)
        {
            return m_ConnectedDot_B;
        }
        else if (dot == m_ConnectedDot_B)
        {
            return m_ConnectedDot_A;
        }

        return null;
    }

    public void CheckForSquares(GameCanvas gameCanvas, out Link[] squareA, out Link[] squareB)
    {
        squareA = null;
        squareB = null;

        if (m_ConnectedDot_A == null || m_ConnectedDot_B == null)
        {
            return;
        }
        
        Debug.Log($"90deg: Checking for squares between ({m_ConnectedDot_A.m_GridPosition.x}, {m_ConnectedDot_A.m_GridPosition.y}) and ({m_ConnectedDot_B.m_GridPosition.x}, {m_ConnectedDot_B.m_GridPosition.y})");
        squareA = FollowLinks(m_ConnectedDot_A, m_ConnectedDot_B, 90.0f);

        Debug.Log($"-90deg: Checking for squares between ({m_ConnectedDot_A.m_GridPosition.x}, {m_ConnectedDot_A.m_GridPosition.y}) and ({m_ConnectedDot_B.m_GridPosition.x}, {m_ConnectedDot_B.m_GridPosition.y})");
        squareB = FollowLinks(m_ConnectedDot_A, m_ConnectedDot_B, -90.0f);
    }

    public Link[] FollowLinks(Dot dotA, Dot dotB, float angle)
    {
        Link[] links = new Link[4];

        Vector2Int dir = new Vector2Int(
            dotB.m_GridPosition.x - dotA.m_GridPosition.x,
            dotB.m_GridPosition.y - dotA.m_GridPosition.y
        );

        Dot dot = dotA;

        Link link = LinkFromDir(dir, dotA);
        if(link != null)
        {
            links[0] = link;
            dot = link.GetOtherDot(dot);
        }

        for(int i = 1; i < 4; i++)
        {
            dir = RotateVector(dir, angle);

            Debug.Log($"dir {i}: ({dir.x}, {dir.y})");

            link = LinkFromDir(dir, dot);
            if(link != null)
            {
                links[i] = link;
                dot = link.GetOtherDot(dot);
                if (dot == null) { Debug.Log("Dot is null"); break; }
            }
            else
            {
                Debug.Log("Link is null");
                break;
            }
        }

        return links;
    }

    public Link LinkFromDir(Vector2Int dir, Dot dot)
    {
        if (dir == Vector2Int.up)
        {
            Debug.Log("LinkFromDir: up");
            return dot.GetLinkUp();
        }
        else if (dir == Vector2Int.down)
        {
            Debug.Log("LinkFromDir: down");
            return dot.GetLinkDown();
        }
        else if (dir == Vector2Int.left)
        {
            Debug.Log("LinkFromDir: left");
            return dot.GetLinkLeft();
        }
        else if (dir == Vector2Int.right)
        {
            Debug.Log("LinkFromDir: right");
            return dot.GetLinkRight();
        }
        else
        {
            Debug.Log("LinkFromDir: null");
            return null;
        }
    }

    public Vector2Int RotateVector(Vector2 v, float angle)
    {
        float radian = angle * Mathf.Deg2Rad;
        float sinAngle = Mathf.Sin(radian);
        float cosAngle = Mathf.Cos(radian);

        int newX = Mathf.RoundToInt(v.x * cosAngle - v.y * sinAngle);
        int newY = Mathf.RoundToInt(v.x * sinAngle + v.y * cosAngle);

        return new Vector2Int(newX, newY);
    }

    public bool IsValidSquare(Link[] links)
    {
        if(links == null)
        {
            return false;
        }

        for(int i = 0; i < 4; i++)
        {
            if(links[i] == null) return false;
            Debug.Log($"Link {i}: ({links[i].m_ConnectedDot_A.m_GridPosition.x}, {links[i].m_ConnectedDot_A.m_GridPosition.y}) -> ({links[i].m_ConnectedDot_B.m_GridPosition.x}, {links[i].m_ConnectedDot_B.m_GridPosition.y})");
        }

        return true;
    }

    public GameCanvas GetGameCanvas()
    {
        if(this.m_ConnectedDot_A != null)
        {
            return this.m_ConnectedDot_A.GetGameCanvas();
        }

        if(this.m_ConnectedDot_B != null)
        {
            return this.m_ConnectedDot_B.GetGameCanvas();
        }

        return null;
    }

    public Game GetGame()
    {
        if(this.m_ConnectedDot_A != null)
        {
            return this.m_ConnectedDot_A.GetGame();
        }

        if(this.m_ConnectedDot_B != null)
        {
            return this.m_ConnectedDot_B.GetGame();
        }

        return null;
    }

    public void Delete()
    {
        if (this == null || this.gameObject == null) { return; }

        // Add a tag or property to indicate that this object is being deleted
        this.m_IsBeingDeleted = true;

        if (this.m_LinkedBoxA != null && !this.m_LinkedBoxA.m_IsBeingDeleted)
        {
            this.m_LinkedBoxA.Delete();
            this.m_LinkedBoxA = null;
        }

        if (this.m_LinkedBoxB != null && !this.m_LinkedBoxB.m_IsBeingDeleted)
        {
            this.m_LinkedBoxB.Delete();
            this.m_LinkedBoxB = null;
        }

        Destroy(this.gameObject);
    }
}