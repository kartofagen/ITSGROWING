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
    [Range(1, 5000)] public int maxBranches = 600;

    [Header("Falloff")]
    [Range(0.5f, 0.95f)] public float radiusDecay = 0.82f;
    [Range(0.5f, 0.98f)] public float lengthDecay = 0.90f;

    [Header("Branching Control (distance-based)")]
    [Tooltip("No branching before this normalized progress along a branch (0..1).")]
    [Range(0f, 1f)] public float branchStart01 = 0.2f;

    [Tooltip("Ensure at least minBranchesByGuarantee branches by this trunk progress (0..1).")]
    [Range(0f, 1f)] public float branchGuarantee01 = 0.5f;

    [Range(0, 1000)] public int minBranchesByGuarantee = 3;

    [Tooltip("Minimum world distance between branch spawn events along a branch.")]
    [Range(0f, 10f)] public float branchInterval = 0.6f;

    [Tooltip("Chance to branch when eligible after branchStart01.")]
    [Range(0f, 1f)] public float branchChanceAfterStart = 0.35f;

    [Range(1, 5)] public int minChildrenPerEvent = 1;
    [Range(1, 5)] public int maxChildrenPerEvent = 2;

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

    [Header("Bounds (Porous Sphere)")]
    public bool usePorousSphere = false;
    public Vector3 porousSphereCenter = Vector3.zero;
    public float porousSphereRadius = 6f;
    [Range(10, 500)] public int fibonacciPointCount = 50;
    [Range(0f, 1f)] public float poreDepth = 0.3f;

    public enum PorousSphereOutMode { ClampToSurfaceAndSlide, ClampToSurfaceStop, StopBranch }
    public PorousSphereOutMode porousSphereOutMode = PorousSphereOutMode.ClampToSurfaceAndSlide;

    [Range(0f, 1f)] public float porousSphereSlideStrength = 1f;
    public float porousSphereSurfaceInset = 0.001f;

    [Header("Porous Sphere Debug Mesh")]
    public bool showPorousSphereMesh = false;
    [Range(10, 100)] public int porousSphereMeshLatitudeSegments = 30;
    [Range(10, 100)] public int porousSphereMeshLongitudeSegments = 30;

    [Header("Bounds (Perpendicular Shell)")]
    public bool usePerpendicularShell = false;
    public Vector3 perpendicularShellCenter = Vector3.zero;
    public float perpendicularShellInnerRadius = 3f;
    public float perpendicularShellOuterRadius = 6f;
    [Range(0f, 1f)] public float perpendicularShellSlideStrength = 1f;
    [Range(0f, 45f)] public float perpendicularShellWiggleDegrees = 15f;
    public float perpendicularShellSurfaceInset = 0.001f;

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
    private int spawnedBranchCount;
    private Vector3 rootPosition; // Store root position for target attraction calculations
    private float trunkTotalLength = 0f; // Total length of the trunk (depth 0)

    // Constraints
    private SphereBoundsConstraint sphereConstraint;
    private HemispherePlaneConstraint hemisphereConstraint;
    private ThetaLimitConstraint thetaConstraint;
    private PorousSphereConstraint porousSphereConstraint;
    private PerpendicularShellConstraint perpendicularShellConstraint;

    // Debug mesh for porous sphere
    private MeshFilter porousSphereMeshFilter;
    private MeshRenderer porousSphereMeshRenderer;
    private GameObject porousSphereMeshObject;

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
        spawnedBranchCount = 0;
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

        porousSphereConstraint = usePorousSphere
            ? new PorousSphereConstraint(
                porousSphereCenter,
                porousSphereRadius,
                porousSphereSurfaceInset,
                (PorousSphereConstraint.OutMode)porousSphereOutMode,
                porousSphereSlideStrength,
                fibonacciPointCount,
                poreDepth)
            : null;

        perpendicularShellConstraint = usePerpendicularShell
            ? new PerpendicularShellConstraint(
                perpendicularShellCenter,
                perpendicularShellInnerRadius,
                perpendicularShellOuterRadius,
                perpendicularShellSurfaceInset,
                perpendicularShellSlideStrength)
            : null;

        // Build debug mesh for porous sphere
        UpdatePorousSphereDebugMesh();

        GrowBranch(
            branches,
            startPos: rootPos,
            startDir: rootDir,
            startGlobalDist: 0f,
            depth: 0,
            thisRadius: Mathf.Max(0.0001f, radius),
            thisSegments: segmentsPerBranch,
            thisSegLen: segmentLength,
            extraConstraint: null,
            allowPerpendicularSpawn: true
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
        float thisSegLen,
        BranchConstraint extraConstraint,
        bool allowPerpendicularSpawn
    )
    {
        if (branchCount >= maxBranches) return;
        if (depth > maxDepth) return;
        if (thisSegments < 2) return;
        if (thisRadius < 0.0005f) return;

        branchCount++;
        if (depth > 0) spawnedBranchCount++;

        var pts = new List<Vector3>(thisSegments + 1);
        var dists = new List<float>(thisSegments + 1);

        pts.Add(startPos);
        dists.Add(startGlobalDist);

        Vector3 dir = SafeNormalize(startDir, Vector3.up);
        Vector3 pos = startPos;
        float g = startGlobalDist;

        Vector3 localUp = AnyPerpendicular(dir);
        bool spawnedPerpendicular = false;
        float expectedBranchLength = thisSegments * Mathf.Max(0.0001f, thisSegLen);
        float lastSpawnDist = startGlobalDist - branchInterval;

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

            if (porousSphereConstraint != null)
            {
                var result = porousSphereConstraint.Apply(pos, nextPos, dir);
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

            if (extraConstraint != null)
            {
                var result = extraConstraint.Apply(pos, nextPos, dir);
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

            if (allowPerpendicularSpawn && usePerpendicularShell && perpendicularShellConstraint != null
                && !spawnedPerpendicular && depth < maxDepth)
            {
                float innerR = Mathf.Max(0f, perpendicularShellInnerRadius);
                if (innerR > 1e-6f)
                {
                    Vector3 shellCenter = perpendicularShellCenter;
                    float currentR = (pos - shellCenter).magnitude;
                    float nextR = (nextPos - shellCenter).magnitude;

                    if (currentR < innerR && nextR >= innerR)
                    {
                        if (TryGetSegmentSphereIntersection(pos, nextPos, shellCenter, innerR, out Vector3 hit))
                        {
                            Vector3 n = SafeNormalize(hit - shellCenter, Vector3.up);
                            Vector3 radialDir = n;

                            if (perpendicularShellWiggleDegrees > 0f)
                                radialDir = DeviateDirection(radialDir, perpendicularShellWiggleDegrees);

                            if (branchCount < maxBranches)
                            {
                                int childDepth = depth + 1;

                                float childRadius = thisRadius * radiusDecay * Lerp(0.95f, 1.05f, (float)rng.NextDouble());
                                float childSegLen = thisSegLen * lengthDecay * Lerp(0.9f, 1.1f, (float)rng.NextDouble());
                                int childSegments = Mathf.Max(6, Mathf.RoundToInt(thisSegments * lengthDecay));

                                float hitDist = Vector3.Distance(pos, hit);
                                float childStartDist = g + hitDist;

                                GrowBranch(
                                    outBranches,
                                    hit,
                                    radialDir,
                                    childStartDist,
                                    childDepth,
                                    childRadius,
                                    childSegments,
                                    childSegLen,
                                    perpendicularShellConstraint,
                                    allowPerpendicularSpawn: false
                                );
                                spawnedPerpendicular = true;
                            }
                        }
                    }
                }
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
                float localProgress = expectedBranchLength > 1e-6f
                    ? (g - startGlobalDist) / expectedBranchLength
                    : 1f;
                bool reachedStart = localProgress >= branchStart01;
                bool spacingOk = (g - lastSpawnDist) >= branchInterval;

                bool forceByGuarantee = depth == 0
                    && minBranchesByGuarantee > 0
                    && branchGuarantee01 > 0f
                    && localProgress >= branchGuarantee01
                    && spawnedBranchCount < minBranchesByGuarantee;

                if (reachedStart && spacingOk && (forceByGuarantee || rng.NextDouble() < branchChanceAfterStart))
                {
                    int spawned = SpawnChildren(outBranches, pos, dir, g, depth, thisRadius, thisSegments, thisSegLen);
                    if (spawned > 0)
                        lastSpawnDist = g;
                }
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
        {
            float localProgressEnd = expectedBranchLength > 1e-6f
                ? (g - startGlobalDist) / expectedBranchLength
                : 1f;
            bool reachedStartEnd = localProgressEnd >= branchStart01;
            bool spacingOkEnd = (g - lastSpawnDist) >= branchInterval;

            bool forceByGuarantee = depth == 0
                && minBranchesByGuarantee > 0
                && branchGuarantee01 > 0f
                && localProgressEnd >= branchGuarantee01
                && spawnedBranchCount < minBranchesByGuarantee;

            if (reachedStartEnd && spacingOkEnd && (forceByGuarantee || rng.NextDouble() < branchChanceAfterStart))
            {
                int spawned = SpawnChildren(outBranches, pos, dir, g, depth, thisRadius, thisSegments, thisSegLen);
                if (spawned > 0)
                    lastSpawnDist = g;
            }
        }
    }

    private int SpawnChildren(
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
        if (branchCount >= maxBranches) return 0;

        int nextDepth = depth + 1;

        float childRadius = parentRadius * radiusDecay * Lerp(0.95f, 1.05f, (float)rng.NextDouble());
        float childSegLen = parentSegLen * lengthDecay * Lerp(0.9f, 1.1f, (float)rng.NextDouble());
        int childSegments = Mathf.Max(6, Mathf.RoundToInt(parentSegments * lengthDecay));

        int minChildren = Mathf.Max(1, minChildrenPerEvent);
        int maxChildren = Mathf.Max(minChildren, maxChildrenPerEvent);
        int childCount = rng.Next(minChildren, maxChildren + 1);

        var dirs = new List<Vector3>(childCount)
        {
            DeviateDirection(parentDir, branchSpreadDegrees * 0.45f)
        };
        if (childCount >= 2)
            dirs.Add(DeviateDirection(parentDir, branchSpreadDegrees));
        if (childCount >= 3)
            dirs.Add(DeviateDirection(parentDir, branchSpreadDegrees * 1.25f));
        while (dirs.Count < childCount)
        {
            float spread = branchSpreadDegrees * Lerp(0.8f, 1.4f, (float)rng.NextDouble());
            dirs.Add(DeviateDirection(parentDir, spread));
        }

        int spawned = 0;
        for (int i = 0; i < dirs.Count; i++)
        {
            if (branchCount >= maxBranches) break;

            float radiusScale = Lerp(0.85f, 1.0f, (float)rng.NextDouble());
            int segs = Mathf.Max(2, childSegments - i);
            GrowBranch(
                outBranches,
                atPos,
                dirs[i],
                atGlobalDist,
                nextDepth,
                childRadius * radiusScale,
                segs,
                childSegLen,
                null,
                true
            );
            spawned++;
        }
        return spawned;
    }

    // helpers
    private static bool TryGetSegmentSphereIntersection(
        Vector3 p0,
        Vector3 p1,
        Vector3 center,
        float radius,
        out Vector3 hit)
    {
        hit = Vector3.zero;
        if (radius <= 0f) return false;

        Vector3 d = p1 - p0;
        float a = Vector3.Dot(d, d);
        if (a < 1e-12f) return false;

        Vector3 m = p0 - center;
        float b = 2f * Vector3.Dot(m, d);
        float c = Vector3.Dot(m, m) - radius * radius;

        float disc = b * b - 4f * a * c;
        if (disc < 0f) return false;

        float sqrtDisc = Mathf.Sqrt(disc);
        float t1 = (-b - sqrtDisc) / (2f * a);
        float t2 = (-b + sqrtDisc) / (2f * a);

        float t = (t1 >= 0f && t1 <= 1f) ? t1 : (t2 >= 0f && t2 <= 1f ? t2 : -1f);
        if (t < 0f) return false;

        hit = p0 + d * t;
        return true;
    }

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

    private void UpdatePorousSphereDebugMesh()
    {
        // Удаляем старый меш, если он существует
        if (porousSphereMeshObject != null)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(porousSphereMeshObject);
            else
                DestroyImmediate(porousSphereMeshObject);
#else
            Destroy(porousSphereMeshObject);
#endif
            porousSphereMeshObject = null;
            porousSphereMeshFilter = null;
            porousSphereMeshRenderer = null;
        }

        // Создаем новый меш, если нужно
        if (showPorousSphereMesh && usePorousSphere && porousSphereConstraint != null)
        {
            // Создаем дочерний GameObject для визуализации меша
            porousSphereMeshObject = new GameObject("PorousSphereDebugMesh");
            porousSphereMeshObject.transform.SetParent(transform);
            porousSphereMeshObject.transform.localPosition = Vector3.zero;
            porousSphereMeshObject.transform.localRotation = Quaternion.identity;
            porousSphereMeshObject.transform.localScale = Vector3.one;

            // Добавляем компоненты
            porousSphereMeshFilter = porousSphereMeshObject.AddComponent<MeshFilter>();
            porousSphereMeshRenderer = porousSphereMeshObject.AddComponent<MeshRenderer>();

            // Строим меш используя метод констрейна (единый источник истины)
            Mesh debugMesh = porousSphereConstraint.BuildSurfaceMesh(
                porousSphereMeshLatitudeSegments,
                porousSphereMeshLongitudeSegments
            );

            porousSphereMeshFilter.mesh = debugMesh;

            // Настраиваем материал (используем стандартный материал или создаем простой)
            if (porousSphereMeshRenderer.material == null)
            {
                Material debugMaterial = new Material(Shader.Find("Standard"));
                debugMaterial.color = new Color(1f, 0f, 0f, 0.3f); // Красный с прозрачностью
                debugMaterial.SetFloat("_Mode", 3); // Transparent mode
                debugMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                debugMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                debugMaterial.SetInt("_ZWrite", 0);
                debugMaterial.DisableKeyword("_ALPHATEST_ON");
                debugMaterial.EnableKeyword("_ALPHABLEND_ON");
                debugMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                debugMaterial.renderQueue = 3000;
                porousSphereMeshRenderer.material = debugMaterial;
            }

            // Делаем меш видимым только в Scene view или в Play mode
            porousSphereMeshRenderer.enabled = true;
        }
    }

}
