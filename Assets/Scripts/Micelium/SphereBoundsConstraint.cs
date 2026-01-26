using UnityEngine;

/// <summary>
/// Ограничение роста веток сферой.
/// </summary>
public class SphereBoundsConstraint : BranchConstraint
{
    public enum OutMode { ClampToSurfaceAndSlide, ClampToSurfaceStop, StopBranch }

    private Vector3 sphereCenter;
    private float sphereRadius;
    private float surfaceInset;
    private OutMode outMode;
    private float slideStrength;

    public SphereBoundsConstraint(
        Vector3 center,
        float radius,
        float inset,
        OutMode mode,
        float slideStr)
    {
        sphereCenter = center;
        sphereRadius = radius;
        surfaceInset = inset;
        outMode = mode;
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

        Vector3 clamped = ProjectInsideSphere(nextPos);

        if ((clamped - nextPos).sqrMagnitude > 1e-12f)
        {
            // Мы вышли за сферу
            result.wasViolated = true;

            if (outMode == OutMode.StopBranch)
            {
                result.newPosition = clamped;
                result.shouldStop = true;
                return result;
            }

            // Приземляемся на поверхность
            result.newPosition = clamped;

            if (outMode == OutMode.ClampToSurfaceStop)
            {
                // Добавим точку и закончим ветку (получится "упёрлась в стенку")
                result.shouldStop = true;
                return result;
            }

            // ClampToSurfaceAndSlide: меняем направление на касательное, чтобы дальше расти вдоль сферы
            result.newDirection = SlideDirectionOnSphere(direction, clamped);
        }

        return result;
    }

    private Vector3 ProjectInsideSphere(Vector3 p)
    {
        float R = Mathf.Max(1e-6f, sphereRadius - surfaceInset);
        Vector3 v = p - sphereCenter;
        float m = v.magnitude;

        if (m <= R) return p;
        return sphereCenter + (v / m) * R; // на поверхности (или чуть внутри)
    }

    private Vector3 SlideDirectionOnSphere(Vector3 dir, Vector3 posOnOrNearSurface)
    {
        // делаем направление касательным к сфере в точке pos
        Vector3 n = SafeNormalize(posOnOrNearSurface - sphereCenter, Vector3.up); // нормаль сферы
        Vector3 tangent = dir - Vector3.Dot(dir, n) * n; // убрали радиальную компоненту
        tangent = SafeNormalize(tangent, AnyPerpendicular(n));
        // смешиваем: 0 = не трогать, 1 = полностью касательная
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
