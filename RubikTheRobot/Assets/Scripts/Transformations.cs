using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Transformations : MonoBehaviour
{
    public Transform endEffector;
    private List<Transform> transformStack = new List<Transform>(10);

    private void Start()
    {
        Transform curTransform = endEffector;
        while (curTransform) {
            transformStack.Add(curTransform);
            curTransform = curTransform.parent;
        }

        // Minimizing: D = || targetLocation - GetEndEffectorLocation() || will give
        // us the inverse kinematics needed
        Vector3 endEffectorLocation = GetEndEffectorLocation();
        Debug.Log(endEffectorLocation);
    }

    Quaternion GetOrientation(Transform T)
    {
        if(T.localEulerAngles.z > 0)
            return Quaternion.AngleAxis(T.localEulerAngles.z, Vector3.forward);
        else if(T.localEulerAngles.y > 0)
            return Quaternion.AngleAxis(T.localEulerAngles.y, Vector3.up);
        else if(T.localEulerAngles.x > 0)
            return Quaternion.AngleAxis(T.localEulerAngles.x, Vector3.right);
        return Quaternion.identity;
    }

    Vector3 GetEndEffectorLocation()
    {
        Quaternion q = Quaternion.identity;
        Vector3 offset = Vector3.zero;
        Transform curTransform;
        for (int i = transformStack.Count - 1; i >= 0; i--)
        {
            curTransform = transformStack[i];
            offset += q * curTransform.localPosition;
            q *= GetOrientation(curTransform);
        }

        return offset;
    }
}
