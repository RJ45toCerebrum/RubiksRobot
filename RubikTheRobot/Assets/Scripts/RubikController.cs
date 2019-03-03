using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

[RequireComponent(typeof(BoxCollider))]
public class RubikController : MonoBehaviour
{
    public enum RubikPivot {Blue, Red, Green, Yellow, White, Orange, None};

    #region MEMBER_VARIABLES
    // Always 6 pivots for the Rubik's Cube
    private readonly Transform[] cubePivots = new Transform[7];
    // Used List because May increase cube count later
    private List<Transform> cubeTransforms = new List<Transform>(27);
    // cache for the transforms that pass the distance test
    private List<Transform> cubesOnRotationPlane = new List<Transform>(9);

    // Rotation control variables
    private RubikPivot currentPivot;
    private Plane rotationPlane = new Plane();
    public AnimationCurve rotationCurve;
    private bool isFaceRotating = false;
    private Vector3 prevRotationVector;
    private float faceRotationTime;
    private Transform rotationPivot;
    private BoxCollider bc;
    private Quaternion qy, qx;
    public float cubeSpinTime;

    // for vr control
    public bool isVRInteraction = false;
    [SerializeField]
    private Transform rightHand, leftHand;
    public SteamVR_Action_Boolean grabPinchDown;
    public SteamVR_Action_Boolean touchpadDown;
    public SteamVR_Action_Vector2 touchpadLocation;
    #endregion

    #region PROPERTIES
    public bool IsFaceRotating
    {
        get { return isFaceRotating; }
    }

    /// <summary>
    /// Returns the transform of the current pivot used for rotation.
    /// DO NOT modify the position or rotation of this transform!
    /// </summary>
    public Transform CurrentPivot
    {
        get { return cubePivots[(int)currentPivot]; }
    }
    #endregion

    #region UNITY_EVENTS
    void Awake()
    {
        // get rerferences to pivot and cube transforms
        foreach(Transform T in transform)
        {
            if(T.tag.EndsWith("Pivot"))
            {
                if(T.tag.StartsWith("Blue"))
                    cubePivots[(int)RubikPivot.Blue] = T;
                else if(T.tag.StartsWith("Red"))
                    cubePivots[(int)RubikPivot.Red] = T;
                else if (T.tag.StartsWith("Green"))
                    cubePivots[(int)RubikPivot.Green] = T;
                else if (T.tag.StartsWith("Yellow"))
                    cubePivots[(int)RubikPivot.Yellow] = T;
                else if (T.tag.StartsWith("White"))
                    cubePivots[(int)RubikPivot.White] = T;
                else if (T.tag.StartsWith("Orange"))
                    cubePivots[(int)RubikPivot.Orange] = T;
                else if (T.tag == "RotationPivot")
                    rotationPivot = T;
            }
            else
                cubeTransforms.Add(T);
        }

        bc = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        Keyframe lkf = rotationCurve.keys[rotationCurve.keys.Length - 1];
        faceRotationTime = lkf.time;
        //versor = Quaternion.AngleAxis(25.0f * Time.deltaTime, Vector3.one);
    }

    private void Update()
    {
        // mouse input
        if (isVRInteraction)
            VRInteraction();
        else
            MouseInteraction();
    }
    #endregion

    #region METHODS
    private void MouseInteraction()
    {
        // If the face is not rotation then check if player is trying to spin a face
        if (!isFaceRotating)
        {
            if (Input.GetMouseButtonDown(0))
            {
                // according to where the player is clicking the cube,
                // find the best pivot and check if we can rotate it
                currentPivot = FindBestRotationPivot();
                if (!CanRotate(currentPivot) || currentPivot == RubikPivot.None)
                {
                    isFaceRotating = false;
                    return;
                }

                Transform cp = CurrentPivot;
                isFaceRotating = true;
                Vector3 pop = MousePositionToPlane(cp);
                prevRotationVector = cp.position - pop;

                rotationPivot.position = cp.position;
                rotationPivot.rotation = cp.rotation;
                FindPivotControlledCubes(rotationPivot);
                ParentUnderRotationPivot(rotationPivot);
            }
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 pop = MousePositionToPlane(rotationPivot);
            Vector3 v = rotationPivot.position - pop;
            float degs = Vector3.SignedAngle(v, prevRotationVector, rotationPivot.forward);
            prevRotationVector = v;

            // lets first get the transform forward in Cube space
            Vector3 spinAxis = transform.InverseTransformDirection(rotationPivot.forward);
            rotationPivot.localRotation = Quaternion.AngleAxis(-degs, spinAxis) * rotationPivot.localRotation;
        }
        else if (Input.GetMouseButtonUp(0))
            StartRotateFaceToNearest90(currentPivot);
    }

    private void VRInteraction()
    {
        if (!isFaceRotating)
        {
            if (touchpadDown.GetStateDown(SteamVR_Input_Sources.Any))
            {
                Vector2 v = touchpadLocation.GetAxis(SteamVR_Input_Sources.Any);
                if (v.magnitude > 0.7f)
                {
                    if (v.x > 0.75f)
                        StartCoroutine(RotateCube90(-transform.up));
                    else if (v.x < -0.75f)
                        StartCoroutine(RotateCube90(transform.up));
                    else if (v.y > 0.75f)
                        StartCoroutine(RotateCube90(transform.right));
                    else
                        StartCoroutine(RotateCube90(-transform.right));
                }
            }
            else if (grabPinchDown.GetStateDown(SteamVR_Input_Sources.Any))
            {
                Transform hand;
                if (grabPinchDown.GetStateDown(SteamVR_Input_Sources.RightHand))
                    hand = rightHand;
                else
                    hand = leftHand;

                Vector3 p = transform.InverseTransformPoint(hand.position);
                if (p.magnitude < 1.2f)
                {
                    // now that it passed the distance test, get closest transform pivot
                    currentPivot = GetClosestPivot(p, false);
                    if (!CanRotate(currentPivot) || currentPivot == RubikPivot.None) {
                        isFaceRotating = false;
                        return;
                    }

                    prevRotationVector = GetHandRotationVector(CurrentPivot);
                    isFaceRotating = true;
                    rotationPivot.position = CurrentPivot.position;
                    rotationPivot.rotation = CurrentPivot.rotation;
                    FindPivotControlledCubes(rotationPivot);
                    ParentUnderRotationPivot(rotationPivot);
                }
            }
        }
        else if (grabPinchDown.GetState(SteamVR_Input_Sources.Any))
        {
            Vector3 crv = GetHandRotationVector(rotationPivot);
            float angle = Vector3.SignedAngle(prevRotationVector, crv, Vector3.forward);
            Vector3 spinAxis = transform.InverseTransformDirection(rotationPivot.forward);
            Quaternion rotator = Quaternion.AngleAxis(angle, spinAxis);
            rotationPivot.localRotation = rotator * rotationPivot.localRotation;
            prevRotationVector = crv;
        }
        else if (grabPinchDown.GetStateUp(SteamVR_Input_Sources.Any))
            StartRotateFaceToNearest90(currentPivot);
    }

    // check if we can rotate the pivot rp, and initiate coroutine for rotating face
    public void StartRotateFace(RubikPivot rp, float degrees)
    {
        if (!CanRotate(rp) || rp == RubikPivot.None)
            return;
        isFaceRotating = true;
        currentPivot = rp;
        StartCoroutine(RotateFace(currentPivot, degrees));
    }

    private IEnumerator RotateFace(RubikPivot rp, float degrees)
    {
        Transform pivot = CurrentPivot;

        /* this checks to see if we should:
            1) reset rotation pivot and 2) find cubes to rotate and 3) parent those cubes
            Reason for this check is because a face may already be rotated and
            have the cubes that its rotating already parented
        */
        if (rotationPivot.childCount == 0) {
            rotationPivot.rotation = pivot.rotation;
            rotationPivot.position = pivot.position;
            FindPivotControlledCubes(rotationPivot);
            ParentUnderRotationPivot(rotationPivot);
        }

        // lets first get the transform forward in cube space
        Vector3 spinAxis = transform.InverseTransformDirection(rotationPivot.forward);
        Quaternion qi = rotationPivot.localRotation;
        Quaternion qf = Quaternion.AngleAxis(degrees, spinAxis) * qi;

        float t = 0, param = 0;
        while (t < faceRotationTime)
        {
            param = rotationCurve.Evaluate(t);
            rotationPivot.localRotation = Quaternion.Slerp(qi, qf, param);
            t += Time.deltaTime;
            yield return null;
        }
        // make rotation perfect 90 and unparent
        rotationPivot.localRotation = qf;
        if(CanUnparent()) {
            UnparentFromRotationPivot();
            currentPivot = RubikPivot.None;
        }
        isFaceRotating = false;
    }

    public void StartRotateFaceToNearest90(RubikPivot rp)
    {
        if (!CanRotate(rp) || rp == RubikPivot.None)
            return;

        currentPivot = rp;
        // get the nearest 90 rotation in local space
        StartCoroutine(RotateFaceToNearest90(currentPivot));
    }

    private IEnumerator RotateFaceToNearest90(RubikPivot rp)
    {
        Transform pivot = cubePivots[(int)rp];
        Quaternion qi = rotationPivot.localRotation;
        Quaternion qf = GetNearest90LocalRotation(pivot);

        FindPivotControlledCubes(rotationPivot);
        ParentUnderRotationPivot(rotationPivot);

        float t = 0, param = 0;
        while (t < faceRotationTime)
        {
            param = rotationCurve.Evaluate(t);
            rotationPivot.localRotation = Quaternion.Slerp(qi, qf, param);
            t += Time.deltaTime;
            yield return null;
        }

        rotationPivot.localRotation = qf;
        UnparentFromRotationPivot();
        isFaceRotating = false;
        currentPivot = RubikPivot.None;
    }

    // more of the same except you may want to have an instant rotation by degrees
    public void RotateFace(float degrees, RubikPivot rp)
    {
        Transform pivot = cubePivots[(int)rp];
        rotationPivot.rotation = pivot.rotation;
        rotationPivot.position = pivot.position;
        FindPivotControlledCubes(rotationPivot);

        Quaternion versor = Quaternion.AngleAxis(degrees, pivot.forward);
        foreach (Transform CT in cubesOnRotationPlane) {
            CT.position = versor * (CT.position - pivot.position) + pivot.position;
            CT.rotation = versor * CT.rotation;
        }
    }

    private IEnumerator RotateCube90(Vector3 axis)
    {
        isFaceRotating = true;
        Quaternion qi = transform.rotation;
        Quaternion qf = Quaternion.AngleAxis(90.0f, axis) * transform.rotation;
        float t = 0;
        while (t <= cubeSpinTime)
        {
            transform.rotation = Quaternion.Slerp(qi, qf, t * (1 / cubeSpinTime));
            t += Time.deltaTime;
            yield return null;
        }
        transform.rotation = qf;
        isFaceRotating = false;
    }

    public bool CanRotate(RubikPivot pivot)
    {
        if (rotationPivot.childCount > 0) {
            if (pivot == currentPivot)
                return true;
        }
        return true;
    }

    private RubikPivot GetClosestPivot(Vector3 op, bool worldSpace = true)
    {
        Transform closestPivot = cubePivots[0];
        RubikPivot cp = RubikPivot.Blue;

        float cd;
        if (worldSpace)
            cd = (op - closestPivot.position).sqrMagnitude;
        else
            cd = (op - closestPivot.localPosition).sqrMagnitude;

        float d = 0;
        for (int i = 1; i < cubePivots.Length - 1; i++)
        {
            if(worldSpace)
                d = (op - cubePivots[i].position).sqrMagnitude;
            else
                d = (op - cubePivots[i].localPosition).sqrMagnitude;

            if (d < cd)
            {
                cd = d;
                closestPivot = cubePivots[i];
                cp = (RubikPivot)i;
            }
        }

        return cp;
    }

    private RubikPivot FindBestRotationPivot()
    {
        Vector3 mp = Input.mousePosition;
        mp.z = Camera.main.nearClipPlane;
        Vector3 p = Camera.main.ScreenToWorldPoint(mp);
        Ray r = new Ray(p, Camera.main.transform.forward);

        RaycastHit hit;
        if (bc.Raycast(r, out hit, 10000))
            return GetClosestPivot(hit.point);

        return RubikPivot.None;
    }
    /*
        Unparenting can happen when the rotation pivot transform
        is at a (n * 90) z rotation relative to the current pivot.
        n is an integer.
     */
    private bool CanUnparent()
    {
        Transform pivot = CurrentPivot;
        Quaternion pivotSpaceRotation = Quaternion.Inverse(pivot.localRotation) * rotationPivot.localRotation;
        float n = Mathf.Abs(pivotSpaceRotation.eulerAngles.z) % 90;
        if (n > 0.001f)
            return false;
        return true;
    }

    private void FindPivotControlledCubes(Transform pivot)
    {
        cubesOnRotationPlane.Clear();
        // The Local Z axis is the spin axis no matter what face... and has length 1
        Vector3 spinAxis = pivot.forward;
        Vector3 v;
        float d;
        for (int i = 0; i < cubeTransforms.Count; i++)
        {
            v = (cubeTransforms[i].position - pivot.position);
            d = Vector3.Dot(v, spinAxis);
            if (Mathf.Abs(d) < 0.01f)
                cubesOnRotationPlane.Add(cubeTransforms[i]);
        }
    }

    private void ParentUnderRotationPivot(Transform pivot)
    {
        foreach(Transform cube in cubesOnRotationPlane)
            cube.SetParent(pivot);
    }

    private void UnparentFromRotationPivot() {
        foreach (Transform cube in cubesOnRotationPlane)
            cube.SetParent(transform);
    }

    private Quaternion GetNearest90LocalRotation(Transform pivot)
    {
        Quaternion rotRelPiv = Quaternion.Inverse(pivot.localRotation) * rotationPivot.localRotation;
        // perform the calculation in this space to get new rotation...
        float closest90 = GetClosestValueTo(rotRelPiv.eulerAngles.z, 90);
        Quaternion pivotSpaceRotation = Quaternion.Euler(0, 0, closest90);
        return pivot.localRotation * pivotSpaceRotation;
    }

    private float GetClosestValueTo(float v, uint t)
    {
        int value = (int)v;
        int p = (int)t;

        int a = Mathf.Abs(value) % p;
        int u = 0, l = 0;
        if(value > 0){
            l = value - a;
            u = l + p;
        }
        else {
            u = value + a;
            l = u - p;
        }

        float avg = (u + l) / 2f;
        if (value > avg)
            return u;

        return l;
    }

    public Vector3 MousePositionToPlane(Transform pivot)
    {
        Vector3 mp = Input.mousePosition;
        mp.z = Camera.main.nearClipPlane;
        Vector3 p = Camera.main.ScreenToWorldPoint(mp);
        Ray r = new Ray(p, Camera.main.transform.forward);
        rotationPlane.SetNormalAndPosition(pivot.forward, pivot.position);
        float t = 0;
        if(rotationPlane.Raycast(r, out t))
            return r.origin + t * r.direction;
        return Vector3.zero;
    }

    private Vector3 GetHandRotationVector(Transform pivot)
    {
        Transform hand;
        if (grabPinchDown.GetState(SteamVR_Input_Sources.RightHand))
            hand = rightHand;
        else
            hand = leftHand;

        Vector3 rv = pivot.InverseTransformVector(hand.right);
        rv.z = 0;
        return rv;
    }
    #endregion
}
