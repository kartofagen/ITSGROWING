// MyceliumTree3D.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MyceliumTree3D : MonoBehaviour
{
    [Header("Tube")]
    public float radius = 0.06f;
    [Range(3, 24)] public int sides = 8;
    public bool capStart = true;
    public bool capEnd = true;

    [Header("Tree")]
    [Range(0, 20)] public int maxDepth = 10;
    [Range(2, 80)] public int segmentsPerBranch = 18;
    public float segmentLength = 0.25f;
    [Range(0f, 1f)] public float wiggle = 0.22f;
    [Range(0f, 90f)] public float branchSpreadDegrees = 35f;
    [Range(0f, 1f)] public float extraBranchChance = 0.55f;
    [Range(0f, 1f)] public float thirdBranchChance = 0.15f;
    [Range(1, 5000)] public int maxBranches = 600;

    [Header("Falloff")]
    [Range(0.5f, 0.95f)] public float radiusDecay = 0.82f;
    [Range(0.5f, 0.98f)] public float lengthDecay = 0.90f;

    [Header("Base branching (no stubs)")]
    [Tooltip("С какой доли ствола разрешаем боковые ветки (0..1). 0.25 = после 25% ствола.")]
    [Range(0f, 1f)] public float trunkBranchStart01 = 0.25f;

    [Tooltip("Сколько гарантированных боковых веток создать на стволе у основания.")]
    [Range(0, 30)] public int guaranteedTrunkBranches = 10;

    [Tooltip("В каком диапазоне ствола (0..1) размещать гарантированные ветки.")]
    [Range(0f, 1f)] public float trunkBranchBand01 = 0.55f;

    [Tooltip("Минимум сегментов у веток на малой глубине (чтобы не были 'пеньками').")]
    [Range(2, 100)] public int baseMinSegments = 18;

    [Tooltip("До какой глубины действует baseMinSegments (0=только ствол, 1=ствол+дети, 2=ещё глубже).")]
    [Range(0, 6)] public int baseBoostDepth = 2;

    [Tooltip("Усиление длины сегмента у основания (1.0 = без изменений).")]
    [Range(1f, 3f)] public float baseSegLenBoost = 1.25f;

    [Header("Bounds (Sphere)")]
    public bool useSphereBounds = true;
    public Vector3 sphereCenter = Vector3.zero;
    public float sphereRadius = 6f;

    public enum SphereOutMode { ClampToSurfaceAndSlide, ClampToSurfaceStop, StopBranch }
    public SphereOutMode sphereOutMode = SphereOutMode.ClampToSurfaceAndSlide;

    [Tooltip("Насколько сильно прижимать направление к касательной при движении по сфере (0..1).")]
    [Range(0f, 1f)] public float slideStrength = 1f;

    [Tooltip("Небольшой отступ внутрь, чтобы избежать дрожания clip/границы (в метрах).")]
    public float surfaceInset = 0.001f;

    [Header("Bounds (Hemisphere)")]
    public bool useHemisphere = true;

    public enum HemispherePlaneMode { ClampAndSlide, ClampStop, StopBranch }
    public HemispherePlaneMode hemispherePlaneMode = HemispherePlaneMode.ClampAndSlide;

    [Range(0f, 1f)] public float planeSlideStrength = 1f;

    [Header("Bounds (Longitude slice)")]
    public bool useThetaLimit = true;

    [Tooltip("Минимальная долгота theta (радианы). 0 = ось +X.")]
    public float thetaMin = 0f;

    [Tooltip("Максимальная долгота theta (радианы). Например 2*PI/3.")]
    public float thetaMax = 2f * Mathf.PI / 3f;

    public enum ThetaOutMode { ClampAndSlide, ClampStop, StopBranch }
    public ThetaOutMode thetaOutMode = ThetaOutMode.ClampAndSlide;

    [Range(0f, 1f)] public float thetaSlideStrength = 1f;




    [Header("Seed")]
    public int seed = 12345;

    [Header("Frame")]
    public TubeMeshBuilder.FrameMode frameMode = TubeMeshBuilder.FrameMode.ParallelTransport;

    [Header("Rebuild")]
    public bool rebuildOnPlay = true;

    private System.Random rng;
    private MeshFilter mf;
    private int branchCount;

    private void Awake()
    {
        mf = GetComponent<MeshFilter>();

        // каждый запуск новый seed
        seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

        if (rebuildOnPlay) Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        rng = new System.Random(seed);
        branchCount = 0;

        var branches = new List<BranchPath>(256);

        Vector3 rootPos = Vector3.zero;
        Vector3 rootDir = Vector3.up;

        sphereCenter = Vector3.zero; // или transform.TransformPoint(...)

        GrowBranch(
            branches,
            startPos: rootPos,
            startDir: rootDir,
            startGlobalDist: 0f,
            depth: 0,
            thisRadius: Mathf.Max(0.0001f, radius),
            thisSegments: segmentsPerBranch,
            thisSegLen: segmentLength
        );

        // Нормализуем глобальную дистанцию 0..1
        float maxDist = 0f;
        foreach (var b in branches)
            if (b.globalDist.Count > 0)
                maxDist = Mathf.Max(maxDist, b.globalDist[b.globalDist.Count - 1]);

        maxDist = Mathf.Max(maxDist, 1e-6f);

        var combines = new List<CombineInstance>(branches.Count);

        foreach (var br in branches)
        {
            // global u по точкам
            var u01 = new float[br.globalDist.Count];
            for (int i = 0; i < u01.Length; i++) u01[i] = br.globalDist[i] / maxDist;

            var mesh = TubeMeshBuilder.Build(
                br.points,
                radius: br.radius,
                sides: sides,
                capStart: capStart,
                capEnd: capEnd,
                smoothNormals: true,
                frameMode: frameMode,
                uByPoint01: u01
            );

            combines.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.identity });
        }

        var combined = new Mesh();
        combined.name = "MyceliumTreeCombined";

        if (EstimateVertexCount(branches) > 65535)
            combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        combined.CombineMeshes(combines.ToArray(), mergeSubMeshes: true, useMatrices: true, hasLightmapData: false);
        combined.RecalculateBounds();

        // подчистим временные меши
        foreach (var ci in combines)
        {
#if UNITY_EDITOR
            if (Application.isPlaying) Destroy(ci.mesh);
            else DestroyImmediate(ci.mesh);
#else
            Destroy(ci.mesh);
#endif
        }

        mf.sharedMesh = combined;
    }

    private int EstimateVertexCount(List<BranchPath> branches)
    {
        long sum = 0;
        foreach (var b in branches) sum += (long)b.points.Count * (long)sides;
        return sum > int.MaxValue ? int.MaxValue : (int)sum;
    }

    private struct BranchPath
    {
        public List<Vector3> points;
        public List<float> globalDist; // расстояние от корня для каждой точки
        public float radius;
    }

    private void GrowBranch(
        List<BranchPath> outBranches,
        Vector3 startPos,
        Vector3 startDir,
        float startGlobalDist,
        int depth,
        float thisRadius,
        int thisSegments,
        float thisSegLen
    )
    {
        if (branchCount >= maxBranches) return;
        if (depth > maxDepth) return;
        if (thisSegments < 2) return;
        if (thisRadius < 0.0005f) return;

        branchCount++;

        var pts = new List<Vector3>(thisSegments + 1);
        var dists = new List<float>(thisSegments + 1);

        pts.Add(startPos);
        dists.Add(startGlobalDist);

        Vector3 dir = SafeNormalize(startDir, Vector3.up);
        Vector3 pos = startPos;
        float g = startGlobalDist;

        Vector3 localUp = AnyPerpendicular(dir);

        for (int i = 0; i < thisSegments; i++)
        {
            Vector3 noise = RandomOnUnitSphere(rng) * wiggle;
            noise -= Vector3.Dot(noise, dir) * dir;

            dir = SafeNormalize(dir + noise + localUp * (float)NextSigned(rng) * wiggle * 0.15f, dir);

            Vector3 nextPos = pos + dir * thisSegLen;

            // --- sphere bounds ---
            if (useSphereBounds)
            {
                Vector3 clamped = ProjectInsideSphere(nextPos);

                if ((clamped - nextPos).sqrMagnitude > 1e-12f)
                {
                    // Мы вышли за сферу
                    if (sphereOutMode == SphereOutMode.StopBranch)
                    {
                        // просто заканчиваем ветку
                        break;
                    }

                    // Приземляемся на поверхность
                    nextPos = clamped;

                    if (sphereOutMode == SphereOutMode.ClampToSurfaceStop)
                    {
                        // добавим точку и закончим ветку (получится "упёрлась в стенку")
                        float stepLenStop = Vector3.Distance(pos, nextPos);
                        pos = nextPos;
                        g += stepLenStop;
                        pts.Add(pos);
                        dists.Add(g);
                        break;
                    }

                    // ClampToSurfaceAndSlide: меняем направление на касательное, чтобы дальше расти вдоль сферы
                    dir = SlideDirectionOnSphere(dir, nextPos);
                }
            }
            // --- end sphere bounds ---
            // --- hemisphere plane (верхняя часть) ---
            if (useHemisphere)
            {
                float planeY = sphereCenter.y;

                if (nextPos.y < planeY)
                {
                    if (hemispherePlaneMode == HemispherePlaneMode.StopBranch)
                    {
                        break;
                    }

                    // Кладём на плоскость
                    nextPos.y = planeY;

                    if (hemispherePlaneMode == HemispherePlaneMode.ClampStop)
                    {
                        float stepLenStop = Vector3.Distance(pos, nextPos);
                        pos = nextPos;
                        g += stepLenStop;
                        pts.Add(pos);
                        dists.Add(g);
                        break;
                    }

                    // Слайдим вдоль плоскости (нормаль вверх)
                    dir = SlideDirectionOnPlane(dir, Vector3.up);
                }
            }
            // --- end hemisphere plane ---
            // --- theta (longitude) limit ---
            if (useThetaLimit)
            {
                float theta = GetTheta(nextPos);

                if (!IsThetaInRange(theta))
                {
                    float boundary = NearestThetaBoundary(theta);

                    if (thetaOutMode == ThetaOutMode.StopBranch)
                        break;

                    nextPos = ClampPointToTheta(nextPos, boundary);

                    if (thetaOutMode == ThetaOutMode.ClampStop)
                    {
                        float stepLenStop = Vector3.Distance(pos, nextPos);
                        pos = nextPos;
                        g += stepLenStop;
                        pts.Add(pos);
                        dists.Add(g);
                        break;
                    }

                    // ClampAndSlide: направляем рост вдоль граничной плоскости
                    dir = SlideDirectionOnThetaPlane(dir, boundary);
                }
            }
            // --- end theta limit ---

            

            float stepLen = Vector3.Distance(pos, nextPos);
            pos = nextPos;
            g += stepLen;

            pts.Add(pos);
            dists.Add(g);


            // иногда ветвим по ходу — и даём детям правильный startGlobalDist = g
            if (depth < maxDepth && i > 2 && i < thisSegments - 3)
            {
                float splitProb = 0.06f * Mathf.Lerp(1.0f, 0.6f, depth / Mathf.Max(1f, maxDepth));
                if (rng.NextDouble() < splitProb)
                    SpawnChildren(outBranches, pos, dir, g, depth, thisRadius, thisSegments, thisSegLen);
            }
        }

        outBranches.Add(new BranchPath { points = pts, globalDist = dists, radius = thisRadius });

        if (depth < maxDepth)
            SpawnChildren(outBranches, pos, dir, g, depth, thisRadius, thisSegments, thisSegLen);
    }

    private void SpawnChildren(
        List<BranchPath> outBranches,
        Vector3 atPos,
        Vector3 parentDir,
        float atGlobalDist,
        int depth,
        float parentRadius,
        int parentSegments,
        float parentSegLen
    )
    {
        if (branchCount >= maxBranches) return;

        int nextDepth = depth + 1;

        float childRadius = parentRadius * radiusDecay * Lerp(0.95f, 1.05f, (float)rng.NextDouble());
        float childSegLen = parentSegLen * lengthDecay * Lerp(0.9f, 1.1f, (float)rng.NextDouble());
        int childSegments = Mathf.Max(6, Mathf.RoundToInt(parentSegments * lengthDecay));

        if (depth <= baseBoostDepth)
        {
            childSegments = Mathf.Max(childSegments, baseMinSegments);
            childSegLen *= baseSegLenBoost;
        }

        // продолжение
        Vector3 d0 = DeviateDirection(parentDir, branchSpreadDegrees * 0.45f);
        GrowBranch(outBranches, atPos, d0, atGlobalDist, nextDepth, childRadius, childSegments, childSegLen);

        // вторая ветка
        if (rng.NextDouble() < extraBranchChance && branchCount < maxBranches)
        {
            Vector3 d1 = DeviateDirection(parentDir, branchSpreadDegrees);
            GrowBranch(outBranches, atPos, d1, atGlobalDist, nextDepth, childRadius * 0.95f, childSegments, childSegLen);
        }

        // третья
        if (rng.NextDouble() < thirdBranchChance && branchCount < maxBranches)
        {
            Vector3 d2 = DeviateDirection(parentDir, branchSpreadDegrees * 1.25f);
            GrowBranch(outBranches, atPos, d2, atGlobalDist, nextDepth, childRadius * 0.9f, childSegments - 2, childSegLen);
        }
    }

    // helpers
    private Vector3 DeviateDirection(Vector3 dir, float maxDegrees)
    {
        dir = SafeNormalize(dir, Vector3.up);
        Vector3 axis = AnyPerpendicular(dir);
        axis = SafeNormalize(axis + RandomOnUnitSphere(rng) * 0.25f, axis);

        float angle = (float)(rng.NextDouble() * 2.0 - 1.0) * maxDegrees;
        Quaternion q = Quaternion.AngleAxis(angle, axis);
        Vector3 outDir = q * dir;

        Quaternion q2 = Quaternion.AngleAxis((float)NextSigned(rng) * maxDegrees * 0.35f, dir);
        outDir = q2 * outDir;

        return SafeNormalize(outDir, dir);
    }

    private static Vector3 AnyPerpendicular(Vector3 v)
    {
        v = SafeNormalize(v, Vector3.up);
        Vector3 a = Mathf.Abs(Vector3.Dot(v, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
        return Vector3.Cross(v, a).normalized;
    }

    private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float m = v.magnitude;
        return (m > 1e-6f) ? (v / m) : fallback;
    }

    private static Vector3 RandomOnUnitSphere(System.Random r)
    {
        double u = r.NextDouble();
        double v = r.NextDouble();
        double theta = 2.0 * Math.PI * u;
        double phi = Math.Acos(2.0 * v - 1.0);
        float x = (float)(Math.Sin(phi) * Math.Cos(theta));
        float y = (float)(Math.Cos(phi));
        float z = (float)(Math.Sin(phi) * Math.Sin(theta));
        return new Vector3(x, y, z);
    }

    private static double NextSigned(System.Random r) => r.NextDouble() * 2.0 - 1.0;
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private Vector3 ProjectInsideSphere(Vector3 p)
    {
        Vector3 c = sphereCenter;
        float R = Mathf.Max(1e-6f, sphereRadius - surfaceInset);
        Vector3 v = p - c;
        float m = v.magnitude;

        if (m <= R) return p;
        return c + (v / m) * R; // на поверхности (или чуть внутри)
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

    private Vector3 SlideDirectionOnPlane(Vector3 dir, Vector3 planeNormal)
    {
        planeNormal = SafeNormalize(planeNormal, Vector3.up);
        Vector3 tangent = dir - Vector3.Dot(dir, planeNormal) * planeNormal;
        tangent = SafeNormalize(tangent, AnyPerpendicular(planeNormal));
        return SafeNormalize(Vector3.Lerp(dir, tangent, planeSlideStrength), tangent);
    }

    static float WrapAngle01(float a) // -> [0, 2PI)
    {
        float twoPi = Mathf.PI * 2f;
        a %= twoPi;
        if (a < 0) a += twoPi;
        return a;
    }

    float GetTheta(Vector3 p)
    {
        Vector3 v = p - sphereCenter;
        return WrapAngle01(Mathf.Atan2(v.z, v.x));
    }

    bool IsThetaInRange(float theta)
    {
        // Поддержка случаев когда диапазон "переваливает" через 0 (wrap)
        float a = WrapAngle01(thetaMin);
        float b = WrapAngle01(thetaMax);
        theta = WrapAngle01(theta);

        if (a <= b) return theta >= a && theta <= b;
        // wrap case, например [300°, 30°]
        return theta >= a || theta <= b;
    }

    float NearestThetaBoundary(float theta)
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

    Vector3 ClampPointToTheta(Vector3 p, float targetTheta)
    {
        Vector3 v = p - sphereCenter;
        float y = v.y;
        float rho = Mathf.Sqrt(v.x * v.x + v.z * v.z); // расстояние до оси Y

        float ct = Mathf.Cos(targetTheta);
        float st = Mathf.Sin(targetTheta);

        Vector3 clamped = new Vector3(rho * ct, y, rho * st);
        return sphereCenter + clamped;
    }

    Vector3 SlideDirectionOnThetaPlane(Vector3 dir, float boundaryTheta)
    {
        // Плоскость-граница проходит через ось Y и направлена по радиусу r=(cosθ,0,sinθ)
        // Нормаль к этой плоскости: n = cross(up, r) = (sinθ, 0, -cosθ)
        float ct = Mathf.Cos(boundaryTheta);
        float st = Mathf.Sin(boundaryTheta);
        Vector3 n = new Vector3(st, 0f, -ct); // нормаль плоскости

        // убираем компоненту вдоль нормали, чтобы скользить вдоль плоскости
        Vector3 tangent = dir - Vector3.Dot(dir, n) * n;
        tangent = SafeNormalize(tangent, AnyPerpendicular(n));

        return SafeNormalize(Vector3.Lerp(dir, tangent, thetaSlideStrength), tangent);
    }




}
