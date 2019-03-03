using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

public class RubikInput : MonoBehaviour
{
    [SerializeField]
    private RubikController rc;
    public int numberOfRotations;
    private RubikController.RubikPivot[] pivots = new RubikController.RubikPivot[6];

    public bool isVRInteraction;

    private Quaternion qy, qx;
    public float rotationSpeed;
    private bool isRotating = false;

    public SteamVR_Action_Single squeezeAction;
    public SteamVR_Action_Boolean grabPinchDown;
    public SteamVR_Action_Vector2 touchpadLocation;
    public SteamVR_Action_Boolean touchpadDown;
    public SteamVR_Action_Boolean gripDownSingle;

    private void Awake() {
        for(uint i = 0; i < pivots.Length; i++)
            pivots[i] = (RubikController.RubikPivot)i;
    }

    void Update ()
    {
        if(isVRInteraction)
        {
            if (!rc.IsFaceRotating && grabPinchDown.GetStateUp(SteamVR_Input_Sources.Any))
                StartCoroutine(ScrambleCube());
            else if(!isRotating && touchpadDown.GetStateDown(SteamVR_Input_Sources.Any))
            {   
                Vector2 v = touchpadLocation.GetAxis(SteamVR_Input_Sources.Any);
                if(v.magnitude > 0.7f)
                {
                    if(v.x > 0.75f)
                        StartCoroutine(Rotate90(-Vector3.up));
                    else if(v.x < -0.75f)
                        StartCoroutine(Rotate90(Vector3.up));
                    else if (v.y > 0.75f)
                        StartCoroutine(Rotate90(Vector3.right));
                    else
                        StartCoroutine(Rotate90(-Vector3.right));
                }
            }
        }
    }

    IEnumerator ScrambleCube()
    {
        RubikController.RubikPivot pivot;
        for(uint i = 0; i < numberOfRotations; i++) {
            pivot = pivots[Random.Range(0, pivots.Length-1)];
            rc.StartRotateFace(pivot, 90);
            yield return new WaitUntil(() => rc.CanRotate(pivot));
        }
    }

    IEnumerator Rotate90(Vector3 axis)
    {
        isRotating = true;
        Quaternion qi = rc.transform.rotation;
        Quaternion qf = Quaternion.AngleAxis(90.0f, axis) * rc.transform.rotation;
        float t = 0;
        while (t <= rotationSpeed)
        {
            rc.transform.rotation = Quaternion.Slerp(qi, qf, t * (1/ rotationSpeed));
            t += Time.deltaTime;
            yield return null;
        }
        rc.transform.rotation = qf;
        isRotating = false;
    }
}
