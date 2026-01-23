using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class MyceliumGrowController : MonoBehaviour
{
    [Range(0f, 1f)] public float grow = 0f;
    public float growSpeed = 0.25f;
    public bool playOnStart = true;
    public bool loop = false;

    static readonly int GrowID = Shader.PropertyToID("_Grow");

    MeshRenderer mr;
    MaterialPropertyBlock mpb;
    float t;

    void Awake()
    {
        mr = GetComponent<MeshRenderer>();
        mpb = new MaterialPropertyBlock();
        t = grow;
        Apply();
    }

    void Update()
    {
        if (!playOnStart) return;

        t += growSpeed * Time.deltaTime;
        if (loop) t = Mathf.Repeat(t, 1f);
        else t = Mathf.Clamp01(t);

        grow = t;
        Apply();
    }

    void Apply()
    {
        mr.GetPropertyBlock(mpb);
        mpb.SetFloat(GrowID, grow);
        mr.SetPropertyBlock(mpb);
    }
}
