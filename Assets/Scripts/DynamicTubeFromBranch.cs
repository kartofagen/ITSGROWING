using System;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DynamicTubeFromBranch : MonoBehaviour
{
    [Header("Input (polyline)")]
    public InextensibleBranchPBD branch; // твой симулятор (Points в локале объекта branch)

    [Header("Tube")]
    public float radius = 0.06f;
    [Range(3, 24)] public int sides = 8;
    public bool capStart = true;
    public bool capEnd = true;

    [Header("Frame")]
    public TubeMeshBuilder.FrameMode frameMode = TubeMeshBuilder.FrameMode.ParallelTransport;
    public Vector3 fixedUp = default;

    [Header("Performance")]
    [Tooltip("Как часто обновлять (1 = каждый кадр, 2 = через кадр и т.д.)")]
    [Range(1, 4)] public int updateEveryNFrames = 1;
    [Tooltip("Обновлять нормали (дороже, но красивее свет). Для Unlit можно выключить.")]
    public bool updateNormals = true;
    [Tooltip("Обновлять bounds вручную (дешевле, чем RecalculateBounds), но нужно задавать запас.")]
    public bool manualBounds = true;
    public float boundsPadding = 0.5f;

    Mesh _mesh;
    MeshFilter _mf;

    int _rings;        // pointsCount
    int _ringVerts;    // sides
    int _bodyVertCount;
    int _capStartVertCount;
    int _capEndVertCount;
    int _vertCount;

    Vector3[] _vertices;
    Vector3[] _normals;
    Vector2[] _uvs;
    Vector4[] _tangents;
    int[] _indices;

    // временные буферы для рамок
    Vector3[] _T;
    Vector3[] _N;
    Vector3[] _B;

    // окружность в 2D
    Vector2[] _circle;

    int _frameCounter;

    void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        if (fixedUp == default) fixedUp = Vector3.up;
        BuildStaticMesh();
    }

    void OnEnable()
    {
        if (_mesh != null) _mesh.MarkDynamic();
    }

    void LateUpdate()
    {
        if (branch == null) return;
        var pts = branch.Points;
        if (pts == null || pts.Count < 2) return;

        _frameCounter++;
        if (updateEveryNFrames > 1 && (_frameCounter % updateEveryNFrames) != 0) return;

        if (pts.Count != _rings)
        {
            // если поменялось число точек — надо перестроить топологию 1 раз
            BuildStaticMesh();
        }

        UpdateDynamicVertices(pts);
    }

    [ContextMenu("Rebuild Static Mesh")]
    public void BuildStaticMesh()
    {
        if (branch == null || branch.Points == null || branch.Points.Count < 2)
        {
            Debug.LogWarning("DynamicTubeFromBranch: branch has not enough points yet.");
            return;
        }

        _rings = branch.Points.Count;
        _ringVerts = Mathf.Max(3, sides);

        _bodyVertCount = _rings * _ringVerts;
        _capStartVertCount = capStart ? (_ringVerts + 1) : 0;
        _capEndVertCount = capEnd ? (_ringVerts + 1) : 0;

        _vertCount = _bodyVertCount + _capStartVertCount + _capEndVertCount;

        _vertices = new Vector3[_vertCount];
        _normals = new Vector3[_vertCount];
        _uvs = new Vector2[_vertCount];
        _tangents = new Vector4[_vertCount];

        // индексы
        int bodyTriCount = (_rings - 1) * _ringVerts * 2;
        int capTriCount = (capStart ? _ringVerts : 0) + (capEnd ? _ringVerts : 0);
        _indices = new int[(bodyTriCount + capTriCount) * 3];

        // окружность
        _circle = new Vector2[_ringVerts];
        for (int s = 0; s < _ringVerts; s++)
        {
            float a = (s / (float)_ringVerts) * Mathf.PI * 2f;
            _circle[s] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        // uv (глобальный u.x всё равно придёт из генератора дерева/роста;
        // для одной верёвки здесь просто 0..1 по длине. Если хочешь — можно прокинуть извне.)
        // Здесь сделаем равномерно по индексу точки:
        for (int i = 0; i < _rings; i++)
        {
            float u = i / (float)(_rings - 1);
            for (int s = 0; s < _ringVerts; s++)
            {
                int vtx = i * _ringVerts + s;
                float v = s / (float)_ringVerts;
                _uvs[vtx] = new Vector2(u, v);
                _tangents[vtx] = new Vector4(1, 0, 0, 1); // уточним в апдейте
            }
        }

        // индексы тела
        int idx = 0;
        for (int i = 0; i < _rings - 1; i++)
        {
            int ring0 = i * _ringVerts;
            int ring1 = (i + 1) * _ringVerts;

            for (int s = 0; s < _ringVerts; s++)
            {
                int s1 = (s + 1) % _ringVerts;

                int a = ring0 + s;
                int b = ring1 + s;
                int c = ring1 + s1;
                int d = ring0 + s1;

                _indices[idx++] = a; _indices[idx++] = b; _indices[idx++] = c;
                _indices[idx++] = a; _indices[idx++] = c; _indices[idx++] = d;
            }
        }

        // капы (только индексы + uv будут выставлены, позиции обновятся в UpdateDynamicVertices)
        int vBase = _bodyVertCount;

        if (capStart)
        {
            int capBase = vBase;
            int center = capBase + _ringVerts;

            for (int s = 0; s < _ringVerts; s++)
            {
                _uvs[capBase + s] = new Vector2(0f, s / (float)_ringVerts);
                _tangents[capBase + s] = new Vector4(1, 0, 0, 1);
            }
            _uvs[center] = new Vector2(0f, 0.5f);

            for (int s = 0; s < _ringVerts; s++)
            {
                int s1 = (s + 1) % _ringVerts;
                _indices[idx++] = center;
                _indices[idx++] = capBase + s1;
                _indices[idx++] = capBase + s;
            }

            vBase += (_ringVerts + 1);
        }

        if (capEnd)
        {
            int capBase = vBase;
            int center = capBase + _ringVerts;

            for (int s = 0; s < _ringVerts; s++)
            {
                _uvs[capBase + s] = new Vector2(1f, s / (float)_ringVerts);
                _tangents[capBase + s] = new Vector4(1, 0, 0, 1);
            }
            _uvs[center] = new Vector2(1f, 0.5f);

            for (int s = 0; s < _ringVerts; s++)
            {
                int s1 = (s + 1) % _ringVerts;
                _indices[idx++] = center;
                _indices[idx++] = capBase + s;
                _indices[idx++] = capBase + s1;
            }
        }

        // буферы рамок
        _T = new Vector3[_rings];
        _N = new Vector3[_rings];
        _B = new Vector3[_rings];

        // создаём Mesh 1 раз
        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.name = "DynamicTube";
            _mesh.MarkDynamic();
        }
        else
        {
            _mesh.Clear();
        }

        if (_vertCount > 65535) _mesh.indexFormat = IndexFormat.UInt32;

        _mesh.vertices = _vertices;       // сейчас пустые — заполним в UpdateDynamicVertices
        _mesh.normals = _normals;
        _mesh.uv = _uvs;
        _mesh.tangents = _tangents;
        _mesh.triangles = _indices;

        _mf.sharedMesh = _mesh;

        // сразу обновим один раз
        UpdateDynamicVertices(branch.Points);
    }

    void UpdateDynamicVertices(System.Collections.Generic.IReadOnlyList<Vector3> pointsLocalToBranch)
    {
        // Мы считаем, что ветка в локале branch, а меш рисуется в локале этого объекта.
        // Самый простой вариант: держать DynamicTubeFromBranch и симулятор ветки на одном объекте.
        // Если нет — можно конвертировать: transform.InverseTransformPoint(branchTransform.TransformPoint(p))
        Transform branchTr = branch.transform;

        // 1) Тангенты
        for (int i = 0; i < _rings; i++)
        {
            Vector3 pPrev = (i == 0) ? pointsLocalToBranch[0] : pointsLocalToBranch[i - 1];
            Vector3 pNext = (i == _rings - 1) ? pointsLocalToBranch[_rings - 1] : pointsLocalToBranch[i + 1];

            Vector3 t = (pNext - pPrev);
            _T[i] = t.sqrMagnitude > 1e-12f ? t.normalized : Vector3.up;
        }

        // 2) Рамки (Parallel Transport — меньше твиста)
        if (frameMode == TubeMeshBuilder.FrameMode.FixedUp)
        {
            for (int i = 0; i < _rings; i++)
            {
                Vector3 up = fixedUp;
                if (Mathf.Abs(Vector3.Dot(up.normalized, _T[i])) > 0.98f)
                    up = Mathf.Abs(Vector3.Dot(Vector3.right, _T[i])) < 0.98f ? Vector3.right : Vector3.forward;

                var q = Quaternion.LookRotation(_T[i], up);
                _B[i] = (q * Vector3.right).normalized;
                _N[i] = Vector3.Cross(_T[i], _B[i]).normalized;
            }
        }
        else
        {
            Vector3 n0 = Vector3.Cross(_T[0], fixedUp);
            if (n0.sqrMagnitude < 1e-8f) n0 = Vector3.Cross(_T[0], Vector3.right);
            if (n0.sqrMagnitude < 1e-8f) n0 = Vector3.Cross(_T[0], Vector3.forward);

            _N[0] = n0.normalized;
            _B[0] = Vector3.Cross(_T[0], _N[0]).normalized;

            for (int i = 1; i < _rings; i++)
            {
                Vector3 v = Vector3.Cross(_T[i - 1], _T[i]);
                float vLen = v.magnitude;

                if (vLen < 1e-6f)
                {
                    _N[i] = _N[i - 1];
                    _B[i] = _B[i - 1];
                }
                else
                {
                    Vector3 axis = v / vLen;
                    float angle = Mathf.Atan2(vLen, Vector3.Dot(_T[i - 1], _T[i])) * Mathf.Rad2Deg;
                    Quaternion rot = Quaternion.AngleAxis(angle, axis);

                    _N[i] = (rot * _N[i - 1]).normalized;
                    _B[i] = Vector3.Cross(_T[i], _N[i]).normalized;
                    _N[i] = Vector3.Cross(_B[i], _T[i]).normalized;
                }
            }
        }

        // 3) Заполняем вершины (тело)
        // Преобразуем точки ветки в локал текущего объекта (если объекты разные)
        for (int i = 0; i < _rings; i++)
        {
            Vector3 pWS = branchTr.TransformPoint(pointsLocalToBranch[i]);
            Vector3 pOS = transform.InverseTransformPoint(pWS);

            for (int s = 0; s < _ringVerts; s++)
            {
                int vtx = i * _ringVerts + s;
                Vector2 c = _circle[s];

                Vector3 radial = (_B[i] * c.x + _N[i] * c.y).normalized;
                _vertices[vtx] = pOS + radial * radius;

                if (updateNormals) _normals[vtx] = radial;

                // tangent (приблизительно вдоль обхвата)
                Vector3 t3 = Vector3.Cross(radial, _T[i]).normalized;
                _tangents[vtx] = new Vector4(t3.x, t3.y, t3.z, 1f);
            }
        }

        // 4) Капы (позиции + нормали)
        int vBase = _bodyVertCount;

        if (capStart)
        {
            int capBase = vBase;
            int center = capBase + _ringVerts;

            // копируем кольцо из первого ринга
            for (int s = 0; s < _ringVerts; s++)
            {
                _vertices[capBase + s] = _vertices[s];
                if (updateNormals) _normals[capBase + s] = -_T[0];
            }

            // центр
            Vector3 pWS0 = branchTr.TransformPoint(pointsLocalToBranch[0]);
            Vector3 pOS0 = transform.InverseTransformPoint(pWS0);
            _vertices[center] = pOS0;
            if (updateNormals) _normals[center] = -_T[0];

            vBase += (_ringVerts + 1);
        }

        if (capEnd)
        {
            int capBase = vBase;
            int center = capBase + _ringVerts;

            int lastRing = (_rings - 1) * _ringVerts;

            for (int s = 0; s < _ringVerts; s++)
            {
                _vertices[capBase + s] = _vertices[lastRing + s];
                if (updateNormals) _normals[capBase + s] = _T[_rings - 1];
            }

            Vector3 pWS1 = branchTr.TransformPoint(pointsLocalToBranch[_rings - 1]);
            Vector3 pOS1 = transform.InverseTransformPoint(pWS1);
            _vertices[center] = pOS1;
            if (updateNormals) _normals[center] = _T[_rings - 1];
        }

        // 5) Применяем в Mesh без пересоздания топологии
        _mesh.SetVertices(_vertices); // Unity 2021+ принимает IList/array; если ругнётся — скажи, дам вариант без аллокаций через SetVertexBufferData
        if (updateNormals) _mesh.SetNormals(_normals);
        _mesh.SetTangents(_tangents);

        if (manualBounds)
        {
            // Быстро: bounds по линиям + запас
            Bounds b = new Bounds(_vertices[0], Vector3.zero);
            for (int i = 1; i < _bodyVertCount; i++) b.Encapsulate(_vertices[i]);
            b.Expand(boundsPadding);
            _mesh.bounds = b;
        }
        else
        {
            _mesh.RecalculateBounds();
        }
    }
}
