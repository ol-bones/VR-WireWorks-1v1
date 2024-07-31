
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Bomb : UdonSharpBehaviour
{
    public bool m_AttachedToHand = false;
    public VRCPlayerApi m_Player;
    public int m_Seat;

    public bool m_IsPlaced = false;
    public Box m_AttachedBox;

    public bool m_IsExploding = false;
    public float m_UpdateDelta = 0;

    public Vector3 m_OriginalScale;
    void Start()
    {
        // this.m_IsPlaced = false;
        // this.m_AttachedBox = null;
        // this.m_Player = null;
        // this.m_Seat = -1;
        // this.m_AttachedToHand = false;
    }

    public void Give(VRCPlayerApi player, int seat)
    {
        this.m_Player = player;
        this.m_Seat = seat;

        this.m_AttachedToHand = true;

        this.m_IsPlaced = false;
        this.m_AttachedBox = null;
    }

    private void Update()
    {
       if(this.m_AttachedToHand)
       {
            this.PositionToHand();
       }
        else if(this.m_IsPlaced)
       {
           this.PositionToBox();
       }

        if(this.m_IsExploding)
        {
            this.m_UpdateDelta += Time.deltaTime;

            float scaleModifier = (1 + Mathf.Sin(Mathf.Abs(this.m_UpdateDelta * 10f)) * 0.1f);
            this.gameObject.transform.localScale = this.m_OriginalScale * scaleModifier;

            float redColorComponentModifier =  Mathf.Sin(Mathf.Abs(this.m_UpdateDelta * 20f));
            this.SetColor(new Color(1 * redColorComponentModifier, 0, 0));
        }
    }

    public void PositionToHand()
    {
        
        if(this.m_Player == null) return;
        VRCPlayerApi.TrackingData rightHandData = this.m_Player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
        // Get the position of the hand
        Vector3 handPosition = rightHandData.position;

        // Get the rotation of the hand
        Quaternion handRotation = rightHandData.rotation;

        // Convert the upward direction from the hand's local coordinate system to the world coordinate system
        Vector3 upwardDirection = handRotation * Vector3.left;

        // Adjust the position of the hand upwards by 0.01 units relative to the hand's orientation
        Vector3 adjustedPosition = handPosition + upwardDirection * 0.025f;

        // Set the position of the laser
        transform.position = adjustedPosition;

        Quaternion offsetRotation = Quaternion.Euler(45, 0, 45);

        // Combine the two rotations
        Quaternion finalRotation = handRotation * offsetRotation;

        // Set the rotation of the laser
        transform.rotation = finalRotation;
    }

    public void PositionToBox()
    {
        if(this.m_AttachedBox == null) { Debug.Log("Bomb.cs: PositionToBox: Attached box is null."); return; }

        this.transform.position = this.m_AttachedBox.transform.position;
        // reset to 0,0,0
        this.transform.rotation = Quaternion.identity; 
    }

    public void PutBombOnBox(Box box)
    {
        if(box == null) { Debug.Log("Bomb.cs: PutBombOnBox: Box is null."); return; }

        this.m_AttachedToHand = false;
        this.m_IsPlaced = true;
        this.m_AttachedBox = box;
    }

    public void BeginExplosion()
    {
        this.m_OriginalScale = new Vector3(
            this.gameObject.transform.localScale.x,
            this.gameObject.transform.localScale.y,
            this.gameObject.transform.localScale.z
        );

        this.m_IsExploding = true;
    }

    public void Explode()
    {
        this.m_IsExploding = false;
        this.gameObject.SetActive(false);
        Vector3 origin = this.transform.position;
        Game game;
        if(this.m_AttachedBox != null)
        {
            Debug.Log($"Bomb.cs: Explode: Attached box is not null: ID: {this.m_AttachedBox.m_ID}, Seat: {this.m_AttachedBox.m_CanvasSeatId}");
            game = this.m_AttachedBox.GetGame();
            if(game == null)
            {
                Debug.Log("Bomb.cs: Explode: this.m_AttachedBox.GetGame() is null.");
                game = this.m_AttachedBox.m_GameRef;
            }
        }
        else
        {
            Debug.Log("Bomb.cs: Explode: Attached box is null.");
            return;
        }
        if(game == null) { Debug.Log("Bomb.cs: Explode: Game is null."); return; }
        if(this.m_AttachedBox == null) { Debug.Log("Bomb.cs: Explode: Attached box is null."); return; }
        GameCanvas canvas = game.GetGameCanvasBySeatId(this.m_AttachedBox.m_CanvasSeatId);
        if(canvas == null) { Debug.Log("Bomb.cs: Explode: Canvas is null."); return; }

        Vector2Int closestDot = new Vector2Int(-1, -1);
        if(this.m_AttachedBox != null)
        {
            Box thisBox = game.GetBoxByIDs(this.m_AttachedBox.m_ID, this.m_AttachedBox.m_CanvasSeatId);
            Debug.Log($"Bomb.cs: Explode: Checking Links");

            Dot dot = GetDotFromLink(thisBox.m_Link_0);
            if (dot == null) dot = GetDotFromLink(thisBox.m_Link_1);
            if (dot == null) dot = GetDotFromLink(thisBox.m_Link_2);
            if (dot == null) dot = GetDotFromLink(thisBox.m_Link_3);

            if (dot != null)
            {
                Debug.Log($"Bomb.cs: Explode: dot is not null: {dot.m_GridPosition.x}, {dot.m_GridPosition.y}");
                closestDot = dot.m_GridPosition;
            }
            Debug.Log($"Bomb.cs: Explode: Closest dot is {closestDot.x}, {closestDot.y}");
        }
        else
        {
            Debug.Log("Bomb.cs: Explode: Attached box is null.");
            return;
        }

        if(closestDot.x == -1 || closestDot.y == -1) { Debug.Log("Bomb.cs: Explode: Closest dot was not set..."); return; }
        
        int count = 0;

        foreach (Dot dot in canvas.m_Dots)
        {
            if (dot != null)
            {
                int distance = Mathf.Abs(dot.m_GridPosition.x - closestDot.x) + Mathf.Abs(dot.m_GridPosition.y - closestDot.y);
                if (distance < game.m_GridSize / 2)
                {
                    count++;
                }
            }
        }

        Dot[] dotsToExplode = new Dot[count];
        int index = 0;

        foreach (Dot dot in canvas.m_Dots)
        {
            if (dot != null)
            {
                int distance = Mathf.Abs(dot.m_GridPosition.x - closestDot.x) + Mathf.Abs(dot.m_GridPosition.y - closestDot.y);
                if (distance < game.m_GridSize / 2)
                {
                    dotsToExplode[index] = dot;
                    index++;
                }
            }
        }

        if (dotsToExplode.Length == 0)
        {
            Debug.Log("No dots to explode");
            return;
        }

        foreach (Dot dot in dotsToExplode)
        {
            if (dot != null)
            {
                Debug.Log($"Exploding dot at {dot.m_GridPosition.x}, {dot.m_GridPosition.y}");
                dot.Reset();
            }
        }


    }
    private Dot GetDotFromLink(Link link)
    {
        if (link == null) 
        {
            Debug.Log("Bomb.cs: Explode: Link is null.");
            return null; 
        }
        Dot dot = link.m_ConnectedDot_A;
        if (dot == null) 
        {
            Debug.Log("Bomb.cs: Explode: dot is null.");
        }
        return dot;
    }

    public void SetColor(Color c)
    {
        Renderer renderer = this.gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = c;
        }
    }
}
