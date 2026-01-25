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
    [Range(2, 200)] public int segmentsPerBranch = 18;
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

    [Header("Target Growth")]
    [Tooltip("Target object that branches will grow towards and surround.")]
    public Transform targetObject;

    [Tooltip("How strongly branches are attracted to the target (0 = no attraction, 1 = strong attraction).")]
    [Range(0f, 1f)] public float targetAttractionStrength = 0.7f;

    [Tooltip("Multiplier for target bounds to determine spread radius around target.")]
    public float targetSpreadRadiusMultiplier = 1.5f;

    [Header("Lights Generation")]
    [Range(0, 20)] public int lightCount = 5;
    [HideInInspector] public List<Vector4> lightSpots = new List<Vector4>();

    [Header("Seed")]
    public int seed = 12345;

    [Header("Frame")]
    public TubeMeshBuilder.FrameMode frameMode = TubeMeshBuilder.FrameMode.ParallelTransport;

    [Header("Rebuild")]
    public bool rebuildOnPlay = true;

    private System.Random rng;
    private MeshFilter mf;
    private int branchCount;
    private Vector3 rootPosition; // Store root position for target attraction calculations
    private float trunkTotalLength = 0f; // Total length of the trunk (depth 0) for trunkBranchStart01 calculation

    // Constraints
    private SphereBoundsConstraint sphereConstraint;
    private HemispherePlaneConstraint hemisphereConstraint;
    private ThetaLimitConstraint thetaConstraint;

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
        trunkTotalLength = 0f; // Reset trunk length

        var branches = new List<BranchPath>(256);

        Vector3 rootPos = Vector3.zero;
        Vector3 rootDir = Vector3.up;
        rootPosition = rootPos; // Store for target attraction calculations

        sphereCenter = Vector3.zero; // или transform.TransformPoint(...)

        // Initialize constraints
        sphereConstraint = useSphereBounds
            ? new SphereBoundsConstraint(
                sphereCenter,
                sphereRadius,
                surfaceInset,
                (SphereBoundsConstraint.OutMode)sphereOutMode,
                slideStrength)
            : null;

        hemisphereConstraint = useHemisphere
            ? new HemispherePlaneConstraint(
                sphereCenter.y,
                (HemispherePlaneConstraint.PlaneMode)hemispherePlaneMode,
                planeSlideStrength)
            : null;

        thetaConstraint = useThetaLimit
            ? new ThetaLimitConstraint(
                sphereCenter,
                thetaMin,
                thetaMax,
                (ThetaLimitConstraint.OutMode)thetaOutMode,
                thetaSlideStrength)
            : null;

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
            // Skip branches with less than 2 points (can happen with StopBranch modes)
            if (br.points == null || br.points.Count < 2)
                continue;

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

        // --- Calculate Light Spots (K-Means) ---
        CalculateLightSpots(branches, maxDist);
    }

    private void CalculateLightSpots(List<BranchPath> branches, float maxDist)
    {
        lightSpots.Clear();
        if (lightCount <= 0) return;

        // 1. Collect all points with their normalized growth time
        var allPoints = new List<Vector4>(); // x,y,z, t(0..1)
        foreach (var b in branches)
        {
            for (int i = 0; i < b.points.Count; i++)
            {
                Vector3 p = b.points[i];
                float t = (b.globalDist.Count > i) ? (b.globalDist[i] / maxDist) : 0f;
                allPoints.Add(new Vector4(p.x, p.y, p.z, t));
            }
        }

        if (allPoints.Count < lightCount)
        {
            // Not enough points, just take what we have
            lightSpots.AddRange(allPoints);
            return;
        }

        // 2. Initialize Centroids (pick random points)
        var centroids = new Vector3[lightCount];
        for (int i = 0; i < lightCount; i++)
        {
            int idx = rng.Next(allPoints.Count);
            centroids[i] = (Vector3)allPoints[idx];
        }

        // 3. K-Means Iterations
        int iterations = 10;
        int[] assignments = new int[allPoints.Count];
        int[] counts = new int[lightCount];
        Vector3[] sums = new Vector3[lightCount];

        for (int it = 0; it < iterations; it++)
        {
            Array.Clear(sums, 0, lightCount);
            Array.Clear(counts, 0, lightCount);

            // Assign points to nearest centroid
            for (int i = 0; i < allPoints.Count; i++)
            {
                Vector3 p = (Vector3)allPoints[i];
                float minDistSq = float.MaxValue;
                int bestC = 0;

                for (int c = 0; c < lightCount; c++)
                {
                    float dSq = (p - centroids[c]).sqrMagnitude;
                    if (dSq < minDistSq)
                    {
                        minDistSq = dSq;
                        bestC = c;
                    }
                }
                assignments[i] = bestC;
                sums[bestC] += p;
                counts[bestC]++;
            }

            // Update centroids
            bool changed = false;
            for (int c = 0; c < lightCount; c++)
            {
                if (counts[c] > 0)
                {
                    Vector3 newPos = sums[c] / counts[c];
                    if ((newPos - centroids[c]).sqrMagnitude > 1e-6f)
                    {
                        centroids[c] = newPos;
                        changed = true;
                    }
                }
                else
                {
                    // If centroid lost all points, re-init to a random point
                    int idx = rng.Next(allPoints.Count);
                    centroids[c] = (Vector3)allPoints[idx];
                    changed = true;
                }
            }

            if (!changed) break;
        }

        // 4. Find nearest actual point for each centroid (to snap to branch and get correct time)
        for (int c = 0; c < lightCount; c++)
        {
            Vector3 centroid = centroids[c];
            float minDistSq = float.MaxValue;
            Vector4 bestPoint = Vector4.zero;

            // Search through all points again to find the closest one to the final centroid
            for (int i = 0; i < allPoints.Count; i++)
            {
                Vector3 p = (Vector3)allPoints[i];
                float dSq = (p - centroid).sqrMagnitude;
                if (dSq < minDistSq)
                {
                    minDistSq = dSq;
                    bestPoint = allPoints[i];
                }
            }
            lightSpots.Add(bestPoint);
        }
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

            Vector3 baseDir = dir + noise + localUp * (float)NextSigned(rng) * wiggle * 0.15f;

            // Target attraction blending
            if (targetObject != null && targetAttractionStrength > 0f)
            {
                bool withinBounds = IsWithinTargetBounds(pos, targetObject);
                float attractionStrength = GetTargetAttractionStrength(pos, targetObject, rootPosition);

                if (attractionStrength > 0f)
                {
                    Vector3 toTarget = (targetObject.position - pos);
                    float distToTarget = toTarget.magnitude;
                    
                    if (distToTarget > 1e-6f)
                    {
                        toTarget /= distToTarget; // Normalize

                        if (withinBounds)
                        {
                            // Within target bounds - apply spreading behavior
                            Vector3 spreadDir = GetTargetSpreadDirection(pos, baseDir, targetObject);
                            // Blend between spreading and slight attraction
                            baseDir = Vector3.Lerp(baseDir, spreadDir, 0.6f);
                            // Still maintain some attraction but weaker
                            baseDir = Vector3.Lerp(baseDir, toTarget, attractionStrength * 0.2f);
                        }
                        else
                        {
                            // Outside bounds - blend attraction with natural growth
                            baseDir = Vector3.Lerp(baseDir, toTarget, attractionStrength);
                        }
                    }
                }
            }

            dir = SafeNormalize(baseDir, dir);

            Vector3 nextPos = pos + dir * thisSegLen;

            // Apply constraints
            if (sphereConstraint != null)
            {
                var result = sphereConstraint.Apply(pos, nextPos, dir);
                if (result.shouldStop)
                {
                    if (result.wasViolated)
                    {
                        // Add the clamped point before stopping
                        float stepLenStop = Vector3.Distance(pos, result.newPosition);
                        pos = result.newPosition;
                        g += stepLenStop;
                        pts.Add(pos);
                        dists.Add(g);
                    }
                    break;
                }
                nextPos = result.newPosition;
                dir = result.newDirection;
            }

            if (hemisphereConstraint != null)
            {
                var result = hemisphereConstraint.Apply(pos, nextPos, dir);
                if (result.shouldStop)
                {
                    if (result.wasViolated)
                    {
                        // Add the clamped point before stopping
                        float stepLenStop = Vector3.Distance(pos, result.newPosition);
                        pos = result.newPosition;
                        g += stepLenStop;
                        pts.Add(pos);
                        dists.Add(g);
                    }
                    break;
                }
                nextPos = result.newPosition;
                dir = result.newDirection;
            }

            if (thetaConstraint != null)
            {
                var result = thetaConstraint.Apply(pos, nextPos, dir);
                if (result.shouldStop)
                {
                    if (result.wasViolated)
                    {
                        // Add the clamped point before stopping
                        float stepLenStop = Vector3.Distance(pos, result.newPosition);
                        pos = result.newPosition;
                        g += stepLenStop;
                        pts.Add(pos);
                        dists.Add(g);
                    }
                    break;
                }
                nextPos = result.newPosition;
                dir = result.newDirection;
            }

            

            float stepLen = Vector3.Distance(pos, nextPos);
            pos = nextPos;
            g += stepLen;

            pts.Add(pos);
            dists.Add(g);

            // Update trunk total length during growth (for depth 0)
            if (depth == 0)
            {
                trunkTotalLength = g;
            }


            // иногда ветвим по ходу — и даём детям правильный startGlobalDist = g
            if (depth < maxDepth && i > 2 && i < thisSegments - 3)
            {
                // Check trunkBranchStart01 for trunk (depth 0) - use segment index as proxy
                if (depth == 0)
                {
                    float segmentProgress = (float)(i + 1) / thisSegments;
                    if (segmentProgress < trunkBranchStart01)
                        continue; // Skip branching if we haven't reached the threshold yet
                }

                float splitProb = 0.06f * Mathf.Lerp(1.0f, 0.6f, depth / Mathf.Max(1f, maxDepth));
                if (rng.NextDouble() < splitProb)
                    SpawnChildren(outBranches, pos, dir, g, depth, thisRadius, thisSegments, thisSegLen);
            }
        }

        // Only add branch if it has at least 2 points (required for TubeMeshBuilder)
        if (pts.Count >= 2)
        {
            outBranches.Add(new BranchPath { points = pts, globalDist = dists, radius = thisRadius });
        }

        // Ensure trunk total length is set before spawning children at the end
        if (depth == 0 && g > 0f)
        {
            trunkTotalLength = g;
        }

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

        // Check trunkBranchStart01: prevent branches from trunk (depth 0) until threshold is reached
        if (depth == 0)
        {
            // Use trunkTotalLength if available, otherwise estimate based on expected segments
            float estimatedTrunkLength = trunkTotalLength > 0f 
                ? trunkTotalLength 
                : (segmentsPerBranch * segmentLength); // Fallback estimate
            
            if (estimatedTrunkLength > 0f)
            {
                float trunkProgress = atGlobalDist / estimatedTrunkLength;
                if (trunkProgress < trunkBranchStart01)
                    return; // Don't spawn branches from trunk until we've reached the threshold
            }
        }

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

    // Target growth helpers
    private bool IsWithinTargetBounds(Vector3 pos, Transform target)
    {
        if (target == null) return false;

        Bounds bounds;
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            bounds = renderer.bounds;
        }
        else
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                bounds = collider.bounds;
            }
            else
            {
                // Fallback: use a default small radius around the target position
                bounds = new Bounds(target.position, Vector3.one * 0.5f);
            }
        }

        // Expand bounds by spread radius multiplier
        bounds.Expand((targetSpreadRadiusMultiplier - 1f) * bounds.size.magnitude);

        return bounds.Contains(pos);
    }

    private float GetTargetAttractionStrength(Vector3 pos, Transform target, Vector3 rootPos)
    {
        if (target == null || targetAttractionStrength <= 0f) return 0f;

        float distanceToTarget = Vector3.Distance(pos, target.position);
        float maxDistance = Vector3.Distance(rootPos, target.position);
        
        if (maxDistance < 1e-6f) return 0f;

        // Stronger attraction when far, weaker when close
        // Use inverse distance ratio, clamped to 0-1
        float distanceRatio = Mathf.Clamp01(distanceToTarget / maxDistance);
        
        // When very close to target, reduce attraction to allow spreading
        Bounds bounds;
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            bounds = renderer.bounds;
        }
        else
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                bounds = collider.bounds;
            }
            else
            {
                bounds = new Bounds(target.position, Vector3.one * 0.5f);
            }
        }

        float targetRadius = bounds.size.magnitude * 0.5f * targetSpreadRadiusMultiplier;
        if (distanceToTarget < targetRadius)
        {
            // Within target bounds - reduce attraction to allow spreading
            float closeRatio = distanceToTarget / targetRadius;
            distanceRatio = Mathf.Lerp(0.1f, distanceRatio, closeRatio);
        }

        return targetAttractionStrength * distanceRatio;
    }

    private Vector3 GetTargetSpreadDirection(Vector3 pos, Vector3 currentDir, Transform target)
    {
        if (target == null) return currentDir;

        Vector3 toTarget = (target.position - pos).normalized;
        
        // Create a perpendicular direction for spreading
        Vector3 perpendicular = Vector3.Cross(toTarget, currentDir);
        if (perpendicular.sqrMagnitude < 1e-6f)
        {
            // If currentDir is parallel to toTarget, use a different perpendicular
            perpendicular = AnyPerpendicular(toTarget);
        }
        perpendicular = SafeNormalize(perpendicular, AnyPerpendicular(toTarget));

        // Add some randomness to the spreading direction
        Vector3 randomComponent = RandomOnUnitSphere(rng);
        randomComponent -= Vector3.Dot(randomComponent, toTarget) * toTarget; // Remove component along toTarget
        randomComponent = SafeNormalize(randomComponent, perpendicular);

        // Blend perpendicular and random for natural spreading
        Vector3 spreadDir = SafeNormalize(perpendicular + randomComponent * 0.5f, perpendicular);
        
        // Blend with current direction to maintain some forward momentum
        return SafeNormalize(Vector3.Lerp(currentDir, spreadDir, 0.4f), currentDir);
    }

}
