using System.Collections.Generic;
using UnityEngine;

/// Неэластичная "ветка/верёвка": длины сегментов постоянные.
/// Интегратор Verlet + PBD-ограничения:
///  - Distance constraints (сохраняем длину)
///  - Bending (жёсткость на изгиб)
///
/// Дальше ты можешь брать positions как polyline для TubeMeshBuilder.
public class InextensibleBranchPBD : MonoBehaviour
{
    [Header("Path")]
    public int pointsCount = 25;
    public float segmentLength = 0.25f;

    [Header("Forces (water)")]
    public Vector3 waterDirection = new Vector3(1, 0, 0);
    public float waterStrength = 1.5f;
    public float turbulence = 0.8f;

    [Header("Dynamics")]
    public float damping = 0.02f;           // 0..0.1
    public int solverIterations = 12;       // 6..20

    [Header("Constraints")]
    [Range(0f, 1f)] public float bendStiffness = 0.25f; // 0=сопля, 1=жёстко
    public AnimationCurve bendByU = AnimationCurve.Linear(0, 1, 1, 1); // можно ослаблять к кончику

    [Header("Pinning")]
    public bool pinRoot = true;

    // Выход: точки ветки в локальных координатах объекта
    public IReadOnlyList<Vector3> Points => _pos;

    List<Vector3> _pos;
    List<Vector3> _prev;
    List<float> _invMass; // 0 = закреплена

    float _time;

    void Awake()
    {
        BuildStraight();
    }

    [ContextMenu("Rebuild Straight")]
    public void BuildStraight()
    {
        _pos = new List<Vector3>(pointsCount);
        _prev = new List<Vector3>(pointsCount);
        _invMass = new List<float>(pointsCount);

        Vector3 p = Vector3.zero;
        Vector3 dir = Vector3.up;

        for (int i = 0; i < pointsCount; i++)
        {
            _pos.Add(p);
            _prev.Add(p);
            _invMass.Add(1f);

            p += dir * segmentLength;
        }

        if (pinRoot && _invMass.Count > 0)
            _invMass[0] = 0f; // корень закреплён
    }

    void Update()
    {
        if (_pos == null || _pos.Count < 2) return;

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        _time += dt;

        // 1) Verlet интеграция (позиция + внешние силы)
        for (int i = 0; i < _pos.Count; i++)
        {
            if (_invMass[i] == 0f) continue;

            Vector3 x = _pos[i];
            Vector3 v = (x - _prev[i]) * (1f - damping);

            // "течение" + турбулентность
            Vector3 flow = waterDirection.normalized * waterStrength;

            // простая процедурная турбулентность (без Perlin, чтобы было проще)
            float n1 = Mathf.Sin(_time * 1.7f + i * 0.73f);
            float n2 = Mathf.Sin(_time * 2.1f + i * 1.11f + 10f);
            float n3 = Mathf.Sin(_time * 1.3f + i * 0.57f + 20f);
            Vector3 noise = new Vector3(n1, n2 * 0.3f, n3) * turbulence;

            Vector3 a = flow + noise;

            // Verlet step
            Vector3 xNew = x + v + a * (dt * dt);

            _prev[i] = x;
            _pos[i] = xNew;
        }

        // 2) PBD solver: несколько итераций ограничений
        for (int it = 0; it < solverIterations; it++)
        {
            SolveDistances();
            SolveBending();
        }

        // если корень закреплён — гарантируем
        if (pinRoot) _pos[0] = Vector3.zero;
    }

    void SolveDistances()
    {
        // Сохраняем длину каждого сегмента
        for (int i = 0; i < _pos.Count - 1; i++)
        {
            int a = i;
            int b = i + 1;

            float wA = _invMass[a];
            float wB = _invMass[b];
            float wSum = wA + wB;
            if (wSum <= 0f) continue;

            Vector3 delta = _pos[b] - _pos[a];
            float dist = delta.magnitude;
            if (dist < 1e-6f) continue;

            float diff = (dist - segmentLength) / dist; // сколько лишнее/нехватка
            Vector3 corr = delta * diff;

            // распределяем по массам
            _pos[a] += corr * (wA / wSum);
            _pos[b] -= corr * (wB / wSum);
        }
    }

    void SolveBending()
    {
        // Простая "жёсткость на изгиб": удерживаем среднюю точку ближе к середине между соседями.
        // Это НЕ меняет длину сегментов (т.к. distance constraints всё равно вернут длины),
        // но сильно влияет на "жёсткость".
        for (int i = 1; i < _pos.Count - 1; i++)
        {
            int prev = i - 1;
            int mid  = i;
            int next = i + 1;

            float wM = _invMass[mid];
            if (wM == 0f) continue;

            float u = i / (float)(_pos.Count - 1);
            float k = bendStiffness * Mathf.Clamp01(bendByU.Evaluate(u));

            if (k <= 0f) continue;

            Vector3 target = 0.5f * (_pos[prev] + _pos[next]);
            Vector3 corr = (target - _pos[mid]) * k;

            _pos[mid] += corr; // можно усложнить распределение на троих, но так достаточно
        }
    }

    // Для визуальной отладки в Scene
    void OnDrawGizmosSelected()
    {
        if (_pos == null || _pos.Count < 2) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < _pos.Count - 1; i++)
        {
            Gizmos.DrawLine(transform.TransformPoint(_pos[i]), transform.TransformPoint(_pos[i + 1]));
        }
    }
}
