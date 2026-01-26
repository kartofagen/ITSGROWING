using UnityEngine;

/// <summary>
/// Ограничение роста веток по долготе (theta/longitude).
/// </summary>
public class ThetaLimitConstraint : BranchConstraint
{
    public enum OutMode { ClampAndSlide, ClampStop, StopBranch }

    private Vector3 sphereCenter;
    private float thetaMin;
    private float thetaMax;
    private OutMode outMode;
    private float slideStrength;

    public ThetaLimitConstraint(
        Vector3 center,
        float min,
        float max,
        OutMode mode,
        float slideStr)
    {
        sphereCenter = center;
        thetaMin = min;
        thetaMax = max;
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

        float theta = GetTheta(nextPos);

        if (!IsThetaInRange(theta))
        {
            result.wasViolated = true;
            float boundary = NearestThetaBoundary(theta);

            if (outMode == OutMode.StopBranch)
            {
                result.newPosition = ClampPointToTheta(nextPos, boundary);
                result.shouldStop = true;
                return result;
            }

            result.newPosition = ClampPointToTheta(nextPos, boundary);

            if (outMode == OutMode.ClampStop)
            {
                result.shouldStop = true;
                return result;
            }

            // ClampAndSlide: направляем рост вдоль граничной плоскости
            result.newDirection = SlideDirectionOnThetaPlane(direction, boundary);
        }

        return result;
    }

    private static float WrapAngle01(float a) // -> [0, 2PI)
    {
        float twoPi = Mathf.PI * 2f;
        a %= twoPi;
        if (a < 0) a += twoPi;
        return a;
    }

    private float GetTheta(Vector3 p)
    {
        Vector3 v = p - sphereCenter;
        return WrapAngle01(Mathf.Atan2(v.z, v.x));
    }

    private bool IsThetaInRange(float theta)
    {
        // Поддержка случаев когда диапазон "переваливает" через 0 (wrap)
        float a = WrapAngle01(thetaMin);
        float b = WrapAngle01(thetaMax);
        theta = WrapAngle01(theta);

        if (a <= b) return theta >= a && theta <= b;
        // wrap case, например [300°, 30°]
        return theta >= a || theta <= b;
    }

    private float NearestThetaBoundary(float theta)
    {
        theta = WrapAngle01(theta);
        float a = WrapAngle01(thetaMin);
        float b = WrapAngle01(thetaMax);

        // если диапазон без wrap (как у тебя), просто берём ближнюю
        if (a <= b)
        {
            float da = Mathf.Abs(Mathf.DeltaAngle(theta * Mathf.Rad2Deg, a * Mathf.Rad2Deg));
            float db = Mathf.Abs(Mathf.DeltaAngle(theta * Mathf.Rad2Deg, b * Mathf.Rad2Deg));
            return (da <= db) ? a : b;
        }

        // wrap: тоже выбираем ближайшую границу по DeltaAngle
        float da2 = Mathf.Abs(Mathf.DeltaAngle(theta * Mathf.Rad2Deg, a * Mathf.Rad2Deg));
        float db2 = Mathf.Abs(Mathf.DeltaAngle(theta * Mathf.Rad2Deg, b * Mathf.Rad2Deg));
        return (da2 <= db2) ? a : b;
    }

    private Vector3 ClampPointToTheta(Vector3 p, float targetTheta)
    {
        Vector3 v = p - sphereCenter;
        float y = v.y;
        float rho = Mathf.Sqrt(v.x * v.x + v.z * v.z); // расстояние до оси Y

        float ct = Mathf.Cos(targetTheta);
        float st = Mathf.Sin(targetTheta);

        Vector3 clamped = new Vector3(rho * ct, y, rho * st);
        return sphereCenter + clamped;
    }

    private Vector3 SlideDirectionOnThetaPlane(Vector3 dir, float boundaryTheta)
    {
        // Плоскость-граница проходит через ось Y и направлена по радиусу r=(cosθ,0,sinθ)
        // Нормаль к этой плоскости: n = cross(up, r) = (sinθ, 0, -cosθ)
        float ct = Mathf.Cos(boundaryTheta);
        float st = Mathf.Sin(boundaryTheta);
        Vector3 n = new Vector3(st, 0f, -ct); // нормаль плоскости

        // убираем компоненту вдоль нормали, чтобы скользить вдоль плоскости
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
