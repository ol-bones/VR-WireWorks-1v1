
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ScrewDriver : UdonSharpBehaviour
{
    void Start()
    {
    }


    void OnCollisionEnter(Collision collision)
    {
        GameObject collisionObject = collision.gameObject;
        if (collisionObject == null) return;

        Dot dot = collisionObject.GetComponent<Dot>();
        if (dot == null) return;
        
        dot.On_ScrewDriver_CollisionEnter(collision);
    }

    void OnCollisionExit(Collision collision)
    {
        // not implemented
    }
}
