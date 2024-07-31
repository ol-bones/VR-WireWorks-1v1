
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class RespawnPensButton : UdonSharpBehaviour
{
    public GameObject m_BlackPen;
    public GameObject m_BluePen;
    public GameObject m_RedPen;
    public GameObject m_YellowPen;

    private Vector3 m_BlackPenStartPos;
    private Vector3 m_BluePenStartPos;
    private Vector3 m_RedPenStartPos;
    private Vector3 m_YellowPenStartPos;

    private Quaternion m_BlackPenStartRot;
    private Quaternion m_BluePenStartRot;
    private Quaternion m_RedPenStartRot;
    private Quaternion m_YellowPenStartRot;

    void Start()
    {
        if (this.m_BlackPen != null)
        {
            GameObject markerMesh = this.m_BlackPen.transform.Find("Mesh").gameObject;
            if (markerMesh == null) { Debug.LogError("RespawnPensButton.cs: Start: markerMesh is null"); return; }

            this.m_BlackPenStartPos = markerMesh.transform.position;
            this.m_BlackPenStartRot = markerMesh.transform.rotation;
        }

        if (this.m_BluePen != null)
        {
            GameObject markerMesh = this.m_BluePen.transform.Find("Mesh").gameObject;
            if (markerMesh == null) { Debug.LogError("RespawnPensButton.cs: Start: markerMesh is null"); return; }

            this.m_BluePenStartPos = markerMesh.transform.position;
            this.m_BluePenStartRot = markerMesh.transform.rotation;
        }

        if (this.m_RedPen != null)
        {
            GameObject markerMesh = this.m_RedPen.transform.Find("Mesh").gameObject;
            if (markerMesh == null) { Debug.LogError("RespawnPensButton.cs: Start: markerMesh is null"); return; }

            this.m_RedPenStartPos = markerMesh.transform.position;
            this.m_RedPenStartRot = markerMesh.transform.rotation;
        }

        if (this.m_YellowPen != null)
        {
            GameObject markerMesh = this.m_YellowPen.transform.Find("Mesh").gameObject;
            if (markerMesh == null) { Debug.LogError("RespawnPensButton.cs: Start: markerMesh is null"); return; }

            this.m_YellowPenStartPos = markerMesh.transform.position;
            this.m_YellowPenStartRot = markerMesh.transform.rotation;
        }
    }

    public override void Interact()
    {
        base.Interact();

        Debug.Log("RespawnPensButton.cs: Interact: Respawn pens");

        if (this.m_BlackPen != null && this.m_BlackPenStartPos != null && m_BlackPenStartPos != Vector3.zero)
        {
            GameObject markerMesh = this.m_BlackPen.transform.Find("Mesh").gameObject;
            if (markerMesh == null) { Debug.LogError("RespawnPensButton.cs: Interact: markerMesh is null"); return; }

            markerMesh.transform.position = this.m_BlackPenStartPos;
            markerMesh.transform.rotation = this.m_BlackPenStartRot;
        }

        if (this.m_BluePen != null && this.m_BluePenStartPos != null && this.m_BluePenStartPos != Vector3.zero)
        {
            GameObject markerMesh = this.m_BluePen.transform.Find("Mesh").gameObject;
            if (markerMesh == null) { Debug.LogError("RespawnPensButton.cs: Interact: markerMesh is null"); return; }

            markerMesh.transform.position = this.m_BluePenStartPos;
            markerMesh.transform.rotation = this.m_BluePenStartRot;
        }

        if (this.m_RedPen != null && this.m_RedPenStartPos != null && this.m_RedPenStartPos != Vector3.zero)
        {
            GameObject markerMesh = this.m_RedPen.transform.Find("Mesh").gameObject;
            if (markerMesh == null) { Debug.LogError("RespawnPensButton.cs: Interact: markerMesh is null"); return; }

            markerMesh.transform.position = this.m_RedPenStartPos;
            markerMesh.transform.rotation = this.m_RedPenStartRot;
        }

        if (this.m_YellowPen != null && this.m_YellowPenStartPos != null && this.m_YellowPenStartPos != Vector3.zero)
        {
            GameObject markerMesh = this.m_YellowPen.transform.Find("Mesh").gameObject;
            if (markerMesh == null) { Debug.LogError("RespawnPensButton.cs: Interact: markerMesh is null"); return; }

            markerMesh.transform.position = this.m_YellowPenStartPos;
            markerMesh.transform.rotation = this.m_YellowPenStartRot;
        }
    }
}
