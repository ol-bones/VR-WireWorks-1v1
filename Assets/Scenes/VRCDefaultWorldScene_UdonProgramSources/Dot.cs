
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

public class Dot : UdonSharpBehaviour
{
    public bool m_Selected;

    public bool m_Highlighted;
    public int m_SeatId = -1;
    public int m_GridSize;
    // bottom left is 0,0 and top right is gridSize-1,gridSize-1
    public Vector2Int m_GridPosition = new Vector2Int(-1, -1);

    public Link m_LinkLeft;
    public Link m_LinkRight;
    public Link m_LinkUp;
    public Link m_LinkDown;

    void Start()
    {
        this.m_Selected = false;
        Debug.Log("Dot Start");
    }

    public override void Interact()
    {
        base.Interact();

        this.m_Selected = !this.m_Selected;
        this.GetGame().Request_DotSelect(this);
    }

    public void On_ScrewDriver_CollisionEnter(Collision collision)
    {
        Game game = this.GetGame();
        if (game != null)
        {
            game.Request_ScrewDriverADot(this);
        }
    }

    public int LinkTo(Dot dot, Color color)
    {
        bool formedSquare = Link.InstantiateLink(this.GetGameCanvas(), this.GetGameCanvas().m_LinkPrefab, this, dot, color, this.GetGameCanvas().m_BoxPrefab);
    
        if(formedSquare)
        {
            return 2;
        }

        return 1;
    }

    public bool IsLinkedTo(Dot dot)
    {
        return this.m_LinkLeft != null && this.m_LinkLeft.GetOtherDot(this) == dot ||
            this.m_LinkRight != null && this.m_LinkRight.GetOtherDot(this) == dot ||
            this.m_LinkUp != null && this.m_LinkUp.GetOtherDot(this) == dot ||
            this.m_LinkDown != null && this.m_LinkDown.GetOtherDot(this) == dot;
    }

    public void Hide()
    {
        UdonBehaviour behaviour = (UdonBehaviour)this.gameObject.GetComponent(typeof(UdonBehaviour));
        if (behaviour != null)
        {
            behaviour.DisableInteractive = true;
        }

        Renderer renderer = this.gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.forceRenderingOff = true;
        }

    }

    public void Show()
    {
        UdonBehaviour behaviour = (UdonBehaviour)this.gameObject.GetComponent(typeof(UdonBehaviour));
        if (behaviour != null)
        {
            behaviour.DisableInteractive = false;
        }

        Renderer renderer = this.gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.forceRenderingOff = false;
        }
    }

    public bool IsVisible()
    {
        Renderer renderer = this.gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            return !renderer.forceRenderingOff;
        }

        return false;
    }

    public void Highlight(Color c)
    {
        Renderer renderer = this.gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = c;
            this.m_Highlighted = true;
        }
    }

    public void Unhighlight()
    {
        Renderer renderer = this.gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.white;
            this.m_Highlighted = false;
        }
    }

    public void SetColor(Color c)
    {
        Renderer renderer = this.gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = c;
        }
    }

    public Link GetLinkLeft()
    {
        return this.m_LinkLeft;
    }

    public Link GetLinkRight()
    {
        return this.m_LinkRight;
    }

    public Link GetLinkUp()
    {
        return this.m_LinkUp;
    }

    public Link GetLinkDown()
    {
        return this.m_LinkDown;
    }

    public bool IsFullyLinked()
    {
        bool isAtLeftEdge = m_GridPosition.x == 0;
        bool isAtRightEdge = m_GridPosition.x == m_GridSize - 1;
        bool isAtBottomEdge = m_GridPosition.y == 0;
        bool isAtTopEdge = m_GridPosition.y == m_GridSize - 1;

        if (isAtLeftEdge && isAtBottomEdge) // Bottom left corner
            return this.m_LinkRight != null && this.m_LinkUp != null;
        if (isAtRightEdge && isAtBottomEdge) // Bottom right corner
            return this.m_LinkLeft != null && this.m_LinkUp != null;
        if (isAtLeftEdge && isAtTopEdge) // Top left corner
            return this.m_LinkRight != null && this.m_LinkDown != null;
        if (isAtRightEdge && isAtTopEdge) // Top right corner
            return this.m_LinkLeft != null && this.m_LinkDown != null;

        if (isAtLeftEdge) // Left edge
            return this.m_LinkRight != null && this.m_LinkUp != null && this.m_LinkDown != null;
        if (isAtRightEdge) // Right edge
            return this.m_LinkLeft != null && this.m_LinkUp != null && this.m_LinkDown != null;
        if (isAtBottomEdge) // Bottom edge
            return this.m_LinkLeft != null && this.m_LinkRight != null && this.m_LinkUp != null;
        if (isAtTopEdge) // Top edge
            return this.m_LinkLeft != null && this.m_LinkRight != null && this.m_LinkDown != null;

        // Not at an edge or corner
        return this.m_LinkLeft != null && this.m_LinkRight != null && this.m_LinkUp != null && this.m_LinkDown != null;
    }

    public Dot GetDotLeft()
    {
        if (this.m_GridPosition.x == 0)
        {
            return null;
        }

        return this.GetGameCanvas().GetDotAtPosition(
            new Vector2Int(
                this.m_GridPosition.x - 1,
                this.m_GridPosition.y
            )
        );
    }

    public Dot GetDotRight()
    {
        if (this.m_GridPosition.x == this.m_GridSize - 1)
        {
            return null;
        }

        return this.GetGameCanvas().GetDotAtPosition(
            new Vector2Int(
                this.m_GridPosition.x + 1,
                this.m_GridPosition.y
            )
        );
    }

    public Dot GetDotUp()
    {
        if (this.m_GridPosition.y == this.m_GridSize - 1)
        {
            return null;
        }

        return this.GetGameCanvas().GetDotAtPosition(
            new Vector2Int(
                this.m_GridPosition.x,
                this.m_GridPosition.y + 1
            )
        );
    }

    public Dot GetDotDown()
    {
        if (this.m_GridPosition.y == 0)
        {
            return null;
        }

        return this.GetGameCanvas().GetDotAtPosition(
            new Vector2Int(
                this.m_GridPosition.x,
                this.m_GridPosition.y - 1
            )
        );
    }

    public GameCanvas GetGameCanvas()
    {
        GameObject currentObject = this.gameObject;
        Transform parentTransform = currentObject.transform.parent;

        if (parentTransform != null)
        {
            GameObject canvasGameObject = parentTransform.gameObject;
            //Debug.Log("Found Canvas GameObject: " + canvasGameObject.name);
            GameCanvas gameCanvas = canvasGameObject.GetComponent<GameCanvas>();
            if (gameCanvas != null)
            {
                return gameCanvas;
            }
        }

        return null;
    }

    // Game
    //    - GameInterface
    //        - Canvas
    //            - Player_Name_Text
    //        - GameCanvas
    //            - Dot
    public Game GetGame()
    {
        GameCanvas gameCanvas = this.GetGameCanvas();
        if (gameCanvas == null)
        {
            Debug.LogError("GameCanvas not found");
            return null;
        }

        GameObject gameCanvasObject = gameCanvas.gameObject;
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

    public void Reset()
    {
        this.m_Selected = false;

        DeleteLink(this.m_LinkLeft);
        DeleteLink(this.m_LinkRight);
        DeleteLink(this.m_LinkUp);
        DeleteLink(this.m_LinkDown);

        this.m_LinkLeft = null;
        this.m_LinkRight = null;
        this.m_LinkUp = null;
        this.m_LinkDown = null;

        this.Hide();
        this.SetColor(Color.white);
    }

    private void DeleteLink(Link link)
    {
        if (link != null && !link.m_IsBeingDeleted)
        {
            link.Delete();
        }
    }
}
