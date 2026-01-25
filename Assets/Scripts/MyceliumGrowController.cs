using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshRenderer))]
public class MyceliumGrowController : MonoBehaviour
{
    [Range(0f, 1f)] public float grow = 0f;
    public float growSpeed = 0.25f;
    public bool playOnStart = true;
    public bool loop = false;

    [Header("Lighting")]
    [Tooltip("Optional: Prefab for the generated lights. If null, a default Point Light is created.")]
    public GameObject lightPrefab;
    public float maxLightIntensity = 2.0f;
    public float maxLightRange = 5.0f;
    public bool syncLightColorWithShader = true;

    static readonly int GrowID = Shader.PropertyToID("_Grow");
    static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");

    MeshRenderer mr;
    MaterialPropertyBlock mpb;
    float t;

    // Internal structure to track spawned lights
    struct SpawnedLight
    {
        public Light light;
        public float triggerTime; // The growth value (0..1) when this light should start appearing
    }
    
    List<SpawnedLight> spawnedLights = new List<SpawnedLight>();
    Color cachedGlowColor = Color.white;

    void Start()
    {
        mr = GetComponent<MeshRenderer>();
        mpb = new MaterialPropertyBlock();
        t = grow;

        if (syncLightColorWithShader && mr.sharedMaterial != null)
        {
            if (mr.sharedMaterial.HasProperty(GlowColorID))
                cachedGlowColor = mr.sharedMaterial.GetColor(GlowColorID);
        }

        SpawnLights();
        Apply();
    }

    void SpawnLights()
    {
        // Clear existing lights if we re-run this (though usually only once in Start)
        foreach (var sl in spawnedLights)
        {
            if (sl.light != null) Destroy(sl.light.gameObject);
        }
        spawnedLights.Clear();

        var tree = GetComponent<MyceliumTree3D>();
        if (tree == null || tree.lightSpots == null) return;

        // Create a sorted copy of light spots by triggerTime
        var sortedSpots = new List<Vector4>(tree.lightSpots);
        sortedSpots.Sort((a, b) => a.w.CompareTo(b.w));

        foreach (var spot in sortedSpots)
        {
            Vector3 pos = new Vector3(spot.x, spot.y, spot.z);
            float triggerTime = spot.w;

            GameObject go;
            Light l = null;

            if (lightPrefab != null)
            {
                go = Instantiate(lightPrefab, transform);
                l = go.GetComponent<Light>();
                if (l == null) l = go.GetComponentInChildren<Light>();
            }
            else
            {
                go = new GameObject("MyceliumLight");
                go.transform.SetParent(transform, false);
                l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.Soft;
            }

            if (go != null)
            {
                go.transform.position = pos;
            }

            if (l != null)
            {
                l.intensity = 0f; // Start hidden
                l.color = cachedGlowColor;
                spawnedLights.Add(new SpawnedLight { light = l, triggerTime = triggerTime });
            }
        }
    }

    void Update()
    {
        if (playOnStart)
        {
            t += growSpeed * Time.deltaTime;
            if (loop) t = Mathf.Repeat(t, 1f);
            else t = Mathf.Clamp01(t);
            grow = t;
        }

        Apply();
    }

    void Apply()
    {
        if (mr == null) return; // Guard against call before Start/Awake
        
        // Ensure MaterialPropertyBlock is initialized
        if (mpb == null) mpb = new MaterialPropertyBlock();

        mr.GetPropertyBlock(mpb);
        mpb.SetFloat(GrowID, grow);
        mr.SetPropertyBlock(mpb);

        // Update all spawned lights with linear interpolation between previous and current positions
        for (int i = 0; i < spawnedLights.Count; i++)
        {
            var sl = spawnedLights[i];
            if (sl.light == null) continue;

            // Get previous light's trigger time (or 0 for the first light)
            float previousTime = (i > 0) ? spawnedLights[i - 1].triggerTime : 0f;
            float currentTime = sl.triggerTime;

            // Calculate linear progress between previous and current light positions
            float progress = 0f;
            if (grow >= previousTime && currentTime > previousTime)
            {
                progress = (grow - previousTime) / (currentTime - previousTime);
                progress = Mathf.Clamp01(progress);
            }
            else if (grow >= currentTime)
            {
                progress = 1f; // Fully lit once we pass this light's position
            }

            sl.light.intensity = progress * maxLightIntensity;
            sl.light.range = 0.5f + (progress * maxLightRange);

            if (syncLightColorWithShader)
            {
                sl.light.color = cachedGlowColor;
            }
        }
    }
}
