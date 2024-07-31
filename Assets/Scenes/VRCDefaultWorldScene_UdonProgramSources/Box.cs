
using System;
using Cysharp.Threading.Tasks.Triggers;
using UdonSharp;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using VRC.SDKBase;
using VRC.Udon;

public class Box : UdonSharpBehaviour
{
    // the seat the canvas belongs to, NOT the player who placed the box
    public int m_CanvasSeatId = -1;
    public string m_ID;
    public Link m_Link_0;
    public Link m_Link_1;
    public Link m_Link_2;
    public Link m_Link_3;

    public Game m_GameRef;
    public bool m_IsBeingDeleted = false;
    void Start()
    {
    }

    public override void Interact()
    {
        base.Interact();
        
        if(this.m_GameRef == null) { Debug.LogError("Box.cs: Interact: Game not found"); return; }

        this.m_GameRef.Request_BoxSelect(this);
    }

    public static bool InstantiateBox(GameCanvas gameCanvas, Link[] links, GameObject boxPrefab, Color color, Link linkedLine)
    {
        Debug.Log("InstantiateBox");
        // Instantiate the box prefab
        GameObject boxObject = UnityEngine.Object.Instantiate(boxPrefab);
        if (boxObject == null)
        {
            Debug.LogError("Box: Failed to instantiate boxPrefab.");
            return false;
        }

        // Get the Box component
        Box box = boxObject.GetComponent<Box>();
        if (box == null)
        {
            Debug.LogError("Box component not found on boxPrefab.");
            return false;
        }

        box.SetLinks(links);

        // Set the position and orientation of the Box object
        box.SetPositionAndOrientation(gameCanvas);
        Game game = box.GetGame();
        if(game == null) return false;
        box.SetGameRef(game);
        
        box.UpdateIDs();

        box.GetComponent<Renderer>().material.color = game.SeatColor(game.m_SeatTurn);

        linkedLine.LinkBox(box);
        game.AddBox(box, game.m_SeatTurn);

        return true;
    }

    public void SetLinks(Link[] links)
    {
        if (links == null || links.Length == 0) { Debug.LogError("Box.cs: SetLinks: Links is null or empty."); return; }

        if (links[0] == null) { Debug.LogError("Box.cs: SetLinks: Link 0 is null."); return; }
        this.m_Link_0 = links[0];
        if (links[1] == null) { Debug.LogError("Box.cs: SetLinks: Link 1 is null."); return; }
        this.m_Link_1 = links[1];
        if (links[2] == null) { Debug.LogError("Box.cs: SetLinks: Link 2 is null."); return; }
        this.m_Link_2 = links[2];
        if (links[3] == null) { Debug.LogError("Box.cs: SetLinks: Link 3 is null."); return; }
        this.m_Link_3 = links[3];
    }

    public void SetGameRef(Game game)
    {
        if(game == null) { Debug.LogError("Box.cs: SetGameRef: Game is null."); return; }
        this.m_GameRef = game;
    }

    public void UpdateIDs()
    {
        this.m_ID = "";
        UpdateIDForLink(m_Link_0);
        UpdateIDForLink(m_Link_1);
        UpdateIDForLink(m_Link_2);
        UpdateIDForLink(m_Link_3);
    }

    private void UpdateIDForLink(Link link)
    {
        if(link == null) { Debug.LogError("Link is null."); return; }
        Dot dotA = link.m_ConnectedDot_A;
        Dot dotB = link.m_ConnectedDot_B;
        if(dotA == null || dotB == null) { Debug.LogError("One or both of the dots are null."); return; }
        this.m_CanvasSeatId = dotA.m_SeatId;
        Debug.Log($"Box.cs: UpdateIDs: CanvasSeatId: {this.m_CanvasSeatId} dotA.m_SeatId: {dotA.m_SeatId} dotB.m_SeatId: {dotB.m_SeatId}");
        this.m_ID += dotA.m_GridPosition.x.ToString() + dotB.m_GridPosition.y.ToString();
    }

    public void SetPositionAndOrientation(GameCanvas gameCanvas)
    {
        float originalWidth = this.transform.localScale.x;
        // Set the rotation of the box based on the direction the canvas is facing
        this.transform.rotation = Quaternion.Euler(90,0,0);
        // Set the parent of the box
        this.transform.SetParent(gameCanvas.transform);// Set the rotation of the box to match the parent's rotation
        this.transform.localRotation = Quaternion.Euler(gameCanvas.transform.forward.y >= 0 ? 90 : 270, 0, 0);

        // Calculate the average position of the dots
        Vector3 position = Vector3.zero;
        position += CalculatePosition(m_Link_0);
        position += CalculatePosition(m_Link_1);
        position += CalculatePosition(m_Link_2);
        position += CalculatePosition(m_Link_3);
        position /= 8; // 4 links * 2 dots per link
        this.transform.position = position;

        // Calculate the distance between the two dots
        float distance = CalculateDistance(m_Link_0) / 10.0f;
        this.transform.localScale = new Vector3(distance, distance, distance);
    }

    private Vector3 CalculatePosition(Link link)
    {
        if (link == null) {
            Debug.LogError("Link is null.");
            return Vector3.zero;
        }
        Dot linkDotA = link.m_ConnectedDot_A;
        Dot linkDotB = link.m_ConnectedDot_B;
        if (linkDotA == null || linkDotB == null) {
            Debug.LogError("One or both of the dots are null.");
            return Vector3.zero;
        }
        return linkDotA.transform.position + linkDotB.transform.position;
    }

    private float CalculateDistance(Link link)
    {
        if (link == null) {
            Debug.LogError("Link is null.");
            return 0f;
        }
        Dot dotA = link.m_ConnectedDot_A;
        Dot dotB = link.m_ConnectedDot_B;
        if (dotA == null || dotB == null) {
            Debug.LogError("One or both of the dots are null.");
            return 0f;
        }
        return Vector3.Distance(dotA.transform.position, dotB.transform.position);
    }
    
    public GameCanvas GetGameCanvas()
    {
        GameCanvas gameCanvas = GetGameCanvasFromLink(m_Link_0);
        if (gameCanvas != null) return gameCanvas;

        gameCanvas = GetGameCanvasFromLink(m_Link_1);
        if (gameCanvas != null) return gameCanvas;

        gameCanvas = GetGameCanvasFromLink(m_Link_2);
        if (gameCanvas != null) return gameCanvas;

        gameCanvas = GetGameCanvasFromLink(m_Link_3);
        return gameCanvas;
    }

    private GameCanvas GetGameCanvasFromLink(Link link)
    {
        if (link == null) return null;
        return link.GetGameCanvas();
    }

    public Game GetGame()
    {
        Game game = GetGameFromLink(m_Link_0);
        if (game != null) return game;

        game = GetGameFromLink(m_Link_1);
        if (game != null) return game;

        game = GetGameFromLink(m_Link_2);
        if (game != null) return game;

        game = GetGameFromLink(m_Link_3);
        return game;
    }

    private Game GetGameFromLink(Link link)
    {
        if (link == null) return null;
        return link.GetGame();
    }

    public void Delete()
    {
        this.m_IsBeingDeleted = true;
        DeleteLink(m_Link_0);
        DeleteLink(m_Link_1);
        DeleteLink(m_Link_2);
        DeleteLink(m_Link_3);

        Destroy(this.gameObject);
    }

    private void DeleteLink(Link link)
    {
        if (link != null)
        {
            link.Delete();
        }
    }
}
