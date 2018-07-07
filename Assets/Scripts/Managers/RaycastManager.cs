using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "RaycastManager")]
public class RaycastManager : Singleton<RaycastManager>
{

    [Header("Raycast settings")]
    public int raycastDistance = 500;

    [Header("Layers")]
    public LayerMask layerMaskGround;

    private RaycastHit[] hitResults = new RaycastHit[1];
    private Plane plane = new Plane(Vector3.up, 0);

    public bool RaycastPlane(Vector2 pos, float height, out Vector3 hitpos)
    {
        var ray = Camera.main.ScreenPointToRay(pos);

        // Raycast against an infinite plane, in case no colliders are present
        float dist;
        if (new Plane(Vector3.down, height).Raycast(ray, out dist))
        {
            hitpos = ray.GetPoint(dist);
            return true;
        }

        hitpos = Vector3.zero;
        return false;
    }

    public bool RaycastGround(Vector2 pos, out Vector3 hitpos)
    {
        var ray = Camera.main.ScreenPointToRay(pos);

        // Raycast against ground layer
        if (Physics.RaycastNonAlloc(ray, hitResults, raycastDistance, layerMaskGround) != 0)
        {
            hitpos = hitResults[0].point;
            return true;
        }

        // Raycast against an infinite plane, in case no colliders are present
        float dist;
        if (plane.Raycast(ray, out dist))
        {
            hitpos = ray.GetPoint(dist);
            return true;
        }

        hitpos = Vector3.zero;
        return false;
    }
}