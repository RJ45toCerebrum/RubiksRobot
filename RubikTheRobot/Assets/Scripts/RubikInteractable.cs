using UnityEngine;
using Valve.VR.InteractionSystem;
using System.Collections.Generic;

[RequireComponent(typeof(RubikController))]
public class RubikInteractable : MonoBehaviour
{
    private Hand.AttachmentFlags attachmentFlags = Hand.defaultAttachmentFlags & (~Hand.AttachmentFlags.SnapOnAttach) & (~Hand.AttachmentFlags.DetachOthers) & (~Hand.AttachmentFlags.VelocityMovement);
    private Interactable interactable;
    private RubikController rc;
    List<GameObject> hideObjects = new List<GameObject>();

    void Awake() {
        interactable = GetComponent<Interactable>();
        rc = GetComponent<RubikController>();
    }

    //-------------------------------------------------
    // Called every Update() while a Hand is hovering over this object
    //-------------------------------------------------
    private void HandHoverUpdate(Hand hand)
    {
        GrabTypes startingGrabType = hand.GetGrabStarting();
        
        bool isGrabEnding = hand.IsGrabEnding(this.gameObject);
        if (interactable.attachedToHand == null && startingGrabType == GrabTypes.Grip)
        {
            hand.HoverLock(interactable);
            hand.AttachObject(gameObject, startingGrabType, attachmentFlags);
        }
        else if (isGrabEnding)
        {
            // Detach this object from the hand
            hand.DetachObject(gameObject);
            hand.HoverUnlock(interactable);
            interactable.hideHighlight = null;
        }
    }
}
