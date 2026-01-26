using UnityEngine;

/// <summary>
/// Ограничение роста веток полуплоскостью (hemisphere plane).
/// </summary>
public class HemispherePlaneConstraint : BranchConstraint
{
    public enum PlaneMode { ClampAndSlide, ClampStop, StopBranch }

    private float planeY;
    private PlaneMode planeMode;
    private float slideStrength;

    public HemispherePlaneConstraint(float y, PlaneMode mode, float slideStr)
    {
        planeY = y;
        planeMode = mode;
        slideStrength = slideStr;
    }

    public override ConstraintResult Apply(Vector3 currentPos, Vector3 nextPos, Vector3 direction)
    {
        var result = new ConstraintResult
        {
            newPosition = nextPos,
            newDirection = direction,
            shouldStop = false,
            wasViolated = false
        };

        if (nextPos.y < planeY)
        {
            result.wasViolated = true;

            if (planeMode == PlaneMode.StopBranch)
            {
                result.newPosition = new Vector3(nextPos.x, planeY, nextPos.z);
                result.shouldStop = true;
                return result;
            }

            // Кладём на плоскость
            result.newPosition = new Vector3(nextPos.x, planeY, nextPos.z);

            if (planeMode == PlaneMode.ClampStop)
            {
                result.shouldStop = true;
                return result;
            }

            // Слайдим вдоль плоскости (нормаль вверх)
            result.newDirection = SlideDirectionOnPlane(direction, Vector3.up);
        }

        return result;
    }

    private Vector3 SlideDirectionOnPlane(Vector3 dir, Vector3 planeNormal)
    {
        planeNormal = SafeNormalize(planeNormal, Vector3.up);
        Vector3 tangent = dir - Vector3.Dot(dir, planeNormal) * planeNormal;
        tangent = SafeNormalize(tangent, AnyPerpendicular(planeNormal));
        return SafeNormalize(Vector3.Lerp(dir, tangent, slideStrength), tangent);
    }

    private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float m = v.magnitude;
        return (m > 1e-6f) ? (v / m) : fallback;
    }

    private static Vector3 AnyPerpendicular(Vector3 v)
    {
        v = SafeNormalize(v, Vector3.up);
        Vector3 a = Mathf.Abs(Vector3.Dot(v, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
        return Vector3.Cross(v, a).normalized;
    }
}
