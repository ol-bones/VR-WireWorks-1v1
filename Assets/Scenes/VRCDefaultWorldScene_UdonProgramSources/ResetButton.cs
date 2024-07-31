
using Miner28.UdonUtils.Network;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ResetButton : NetworkInterface
{
    void Start()
    {
        
    }
    public override void Interact()
    {
        base.Interact();

        Game game = GetGame();
        if (game != null)
        {
            game.Request_ResetGame();
        }
    }

    public Game GetGame()
    {
        GameObject currentObject = this.gameObject;
        Transform parentTransform = currentObject.transform.parent;

        if (parentTransform != null)
        {
            GameObject gameGameObject = parentTransform.gameObject;
            Debug.Log("Found Game GameObject: " + gameGameObject.name);
            Game game = gameGameObject.GetComponent<Game>();
            if (game != null)
            {
                return game;
            }
        }

        return null;
    }
}
