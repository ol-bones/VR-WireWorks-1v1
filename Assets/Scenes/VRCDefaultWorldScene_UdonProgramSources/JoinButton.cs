using Miner28.UdonUtils.Network;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public class JoinButton : NetworkInterface
{
    void Start()
    {
    }
    
    public override void Interact()
    {
        base.Interact();
        
        Player player = FindPlayerGameObject();
        if (player != null)
        {
            VRCPlayerApi localPlayer;
            localPlayer = VRC.SDKBase.Networking.LocalPlayer;
            if(localPlayer != null)
            {
                player.SendMethodNetworked(
                    nameof(Player.On_SeatJoinRequest),
                    SyncTarget.All,
                    new DataToken(Networking.LocalPlayer),
                    new DataToken(player.m_GameSeatId),
                    new DataToken(player.m_GameId)
                );
                
                this.gameObject.SetActive(false);
            }
        }
    }

    private Player FindPlayerGameObject()
    {
        GameObject currentObject = this.gameObject;
        Transform parentTransform = currentObject.transform.parent;

        if (parentTransform != null)
        {
            Transform canvasTransform = parentTransform.Find("Canvas");
            if (canvasTransform != null)
            {
                Transform playerNameTextTransform = canvasTransform.Find("Player_Name_Text");
                if (playerNameTextTransform != null)
                {
                    GameObject playerGameObject = playerNameTextTransform.gameObject;
                    Debug.Log("Found Player_Name_Text GameObject: " + playerGameObject.name);
                    Player player = playerGameObject.GetComponent<Player>();
                    if (player != null)
                    {
                        return player;
                    }
                }
                else
                {
                    Debug.LogError("Player_Name_Text GameObject not found under Canvas.");
                }
            }
            else
            {
                Debug.LogError("Canvas GameObject not found under parent.");
            }
        }
        else
        {
            Debug.LogError("Parent Transform is null.");
        }

        return null;
    }
}
