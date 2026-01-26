using UnityEngine;

/// <summary>
/// Ограничение роста веток сферической оболочкой [r, R].
/// Внутри r — прижимает к внутренней сфере и скользит,
/// за пределами R — прижимает к внешней сфере и останавливает ветку.
/// </summary>
public class PerpendicularShellConstraint : BranchConstraint
{
    private Vector3 shellCenter;
    private float innerRadius;
    private float outerRadius;
    private float surfaceInset;
    private float slideStrength;

    public PerpendicularShellConstraint(
        Vector3 center,
        float inner,
        float outer,
        float inset,
        float slideStr)
    {
        shellCenter = center;
        innerRadius = Mathf.Max(0f, inner);
        outerRadius = Mathf.Max(innerRadius, outer);
        surfaceInset = Mathf.Max(0f, inset);
        slideStrength = Mathf.Clamp01(slideStr);
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

        float inner = Mathf.Max(0f, innerRadius + surfaceInset);
        float outer = Mathf.Max(inner + 1e-6f, outerRadius - surfaceInset);

        Vector3 toNext = nextPos - shellCenter;
        float r = toNext.magnitude;

        if (r < inner - 1e-6f)
        {
            result.wasViolated = true;

            Vector3 n = SafeNormalize(toNext, Vector3.up);
            result.newPosition = shellCenter + n * inner;
            result.newDirection = SlideDirectionOnSphere(direction, n);
            return result;
        }

        if (r > outer + 1e-6f)
        {
            result.wasViolated = true;

            Vector3 n = SafeNormalize(toNext, Vector3.up);
            result.newPosition = shellCenter + n * outer;
            result.shouldStop = true;
            return result;
        }

        return result;
    }

    private Vector3 SlideDirectionOnSphere(Vector3 dir, Vector3 surfaceNormal)
    {
        Vector3 n = SafeNormalize(surfaceNormal, Vector3.up);
        Vector3 tangent = dir - Vector3.Dot(dir, n) * n;
        tangent = SafeNormalize(tangent, AnyPerpendicular(n));
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
