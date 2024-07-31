
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MirrorToggle : UdonSharpBehaviour
{
    public GameObject m_MirrorObject;
    
    void Start()
    {
        
    }

    public override void Interact()
    {
        base.Interact();
        
        if (this.m_MirrorObject == null) { Debug.LogError("MirrorToggle.cs: Interact: mirrorObject is null"); return; }

        if (this.m_MirrorObject.gameObject.activeSelf)
        {
            this.m_MirrorObject.gameObject.SetActive(false);
        }
        else
        {
            this.m_MirrorObject.gameObject.SetActive(true);
        }
    }
}
