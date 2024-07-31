
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class HandLaser : UdonSharpBehaviour
{
    public VRCPlayerApi player;
    public VRCPlayerApi.TrackingDataType handType;

    private void Update()
    {
        if(player == null) player = Networking.LocalPlayer;
        
        VRCPlayerApi.TrackingData rightHandData = player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
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

        // Create a rotation that represents a 35 degree rotation around the X axis
        Quaternion offsetRotation = Quaternion.Euler(0, 40, 0);

        // Combine the two rotations
        Quaternion finalRotation = handRotation * offsetRotation;

        // Set the rotation of the laser
        transform.rotation = finalRotation;
    }
}