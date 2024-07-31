
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Tool_box : UdonSharpBehaviour
{
    void Start()
    {
        
    }

    public override void Interact()
    {
        base.Interact();
        
        ScrewDriver screwDriver = FindScrewDriverGameObject();
        if (screwDriver != null)
        {
            // reset the position of the screwdriver
            screwDriver.transform.position = this.transform.position + new Vector3(0, 1f, 0);
        }
    }

    
    private ScrewDriver FindScrewDriverGameObject()
    {
        GameObject currentObject = this.gameObject;
        Transform parentTransform = currentObject.transform.parent;

        if (parentTransform != null)
        {
            Transform screwdriverTransform = parentTransform.Find("ScrewDriver");
            if (screwdriverTransform != null)
            {
                Debug.Log("Found ScrewDriver GameObject: " + screwdriverTransform.name);
                ScrewDriver screwDriver = screwdriverTransform.GetComponent<ScrewDriver>();
                if (screwDriver != null)
                {
                    return screwDriver;
                }
            }
            else
            {
                Debug.LogError("ScrewDriver GameObject not found under Canvas.");
            }
        }
        else
        {
            Debug.LogError("Parent Transform is null.");
        }

        return null;
    }
}
