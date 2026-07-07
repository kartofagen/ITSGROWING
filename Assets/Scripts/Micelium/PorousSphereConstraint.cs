using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ограничение роста веток пористой сферой с точками Фибоначчи.
/// Поверхность задается как r = R * (p_min + (1 - p_min) * p), где p - нормированное расстояние до ближайшей точки Фибоначчи.
/// </summary>
public class PorousSphereConstraint : BranchConstraint
{
    public enum OutMode { ClampToSurfaceAndSlide, ClampToSurfaceStop, StopBranch }

    private Vector3 sphereCenter;
    private float sphereRadius;
    private float surfaceInset;
    private OutMode outMode;
    private float slideStrength;
    private float poreDepth; // p_min - минимальный радиус пор (0..1)
    private List<Vector3> fibonacciPoints; // Точки Фибоначчи на единичной сфере
    private float maxAngularDistance; // Максимальное угловое расстояние между точками (для нормализации)

    public PorousSphereConstraint(
        Vector3 center,
        float radius,
        float inset,
        OutMode mode,
        float slideStr,
        int fibonacciPointCount,
        float poreDepthParam)
    {
        sphereCenter = center;
        sphereRadius = radius;
        surfaceInset = inset;
        outMode = mode;
        slideStrength = slideStr;
        poreDepth = Mathf.Clamp01(poreDepthParam);

        // Генерируем точки Фибоначчи на единичной сфере
        GenerateFibonacciPoints(fibonacciPointCount);
        CalculateMaxAngularDistance();
    }

    /// <summary>
    /// Генерирует равноудаленные точки на единичной сфере используя алгоритм Фибоначчи.
    /// </summary>
    private void GenerateFibonacciPoints(int count)
    {
        fibonacciPoints = new List<Vector3>(count);
        
        if (count <= 0)
        {
            // Минимум одна точка в центре верхнего полюса
            fibonacciPoints.Add(Vector3.up);
            return;
        }

        if (count == 1)
        {
            fibonacciPoints.Add(Vector3.up);
            return;
        }

        float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f)); // Золотой угол в радианах

        for (int i = 0; i < count; i++)
        {
            float y = 1f - (2f * i) / (count - 1f); // От 1 до -1
            float r = Mathf.Sqrt(1f - y * y); // Радиус на уровне y
            float theta = goldenAngle * i; // Угол поворота

            float x = r * Mathf.Cos(theta);
            float z = r * Mathf.Sin(theta);

            Vector3 point = new Vector3(x, y, z);
            fibonacciPoints.Add(point.normalized); // Нормализуем для точной единичной сферы
        }
    }

    /// <summary>
    /// Вычисляет максимальное угловое расстояние между точками для нормализации.
    /// p должен быть равен 1, когда угловое расстояние равно половине углового расстояния
    /// между двумя соседними точками Фибоначчи.
    /// </summary>
    private void CalculateMaxAngularDistance()
    {
        if (fibonacciPoints.Count < 2)
        {
            maxAngularDistance = Mathf.PI; // Половина окружности
            return;
        }

        // Находим минимальное расстояние от каждой точки до её ближайшего соседа
        float sumMinDistances = 0f;
        int validPoints = 0;

        for (int i = 0; i < fibonacciPoints.Count; i++)
        {
            float minDist = float.MaxValue;
            
            for (int j = 0; j < fibonacciPoints.Count; j++)
            {
                if (i == j) continue;
                
                float dist = AngularDistance(fibonacciPoints[i], fibonacciPoints[j]);
                if (dist < minDist)
                    minDist = dist;
            }
            
            if (minDist < float.MaxValue)
            {
                sumMinDistances += minDist;
                validPoints++;
            }
        }

        if (validPoints > 0)
        {
            // Среднее минимальное расстояние между соседними точками
            float avgMinDistance = sumMinDistances / validPoints;
            // p = 1 когда угловое расстояние равно половине этого расстояния
            maxAngularDistance = avgMinDistance * 0.5f;
        }
        else
        {
            maxAngularDistance = Mathf.PI * 0.5f;
        }
    }

    /// <summary>
    /// Вычисляет угловое расстояние между двумя точками на единичной сфере (в радианах).
    /// </summary>
    private float AngularDistance(Vector3 a, Vector3 b)
    {
        // Угол между векторами: arccos(dot(a, b))
        float dot = Mathf.Clamp(Vector3.Dot(a.normalized, b.normalized), -1f, 1f);
        return Mathf.Acos(dot);
    }

    /// <summary>
    /// Находит ближайшую точку Фибоначчи для заданной позиции (на единичной сфере).
    /// </summary>
    private int FindNearestFibonacciPoint(Vector3 direction)
    {
        direction = direction.normalized;
        int nearestIdx = 0;
        float minDist = float.MaxValue;

        for (int i = 0; i < fibonacciPoints.Count; i++)
        {
            float dist = AngularDistance(direction, fibonacciPoints[i]);
            if (dist < minDist)
            {
                minDist = dist;
                nearestIdx = i;
            }
        }

        return nearestIdx;
    }

    /// <summary>
    /// Вычисляет нормированное расстояние до ближайшей точки Фибоначчи (0 = в точке, 1 = максимально далеко).
    /// </summary>
    private float GetNormalizedDistance(Vector3 direction)
    {
        if (fibonacciPoints.Count == 0)
            return 1f;

        int nearestIdx = FindNearestFibonacciPoint(direction);
        float angularDist = AngularDistance(direction.normalized, fibonacciPoints[nearestIdx]);

        // Нормализуем: 0 = в точке, 1 = максимально далеко
        float normalized = Mathf.Clamp01(angularDist / maxAngularDistance);
        return normalized;
    }

    /// <summary>
    /// Вычисляет допустимый радиус для заданного направления.
    /// </summary>
    private float GetAllowedRadius(Vector3 direction)
    {
        float p = GetNormalizedDistance(direction);
        // Формула: r = R * (p_min + (1 - p_min) * p)
        float allowedRadius = sphereRadius * (poreDepth + (1f - poreDepth) * p);
        return Mathf.Max(0f, allowedRadius - surfaceInset);
    }

    /// <summary>
    /// Строит меш поверхности пористой сферы для дебага.
    /// Использует те же вычисления, что и констрейн, обеспечивая полную синхронизацию.
    /// </summary>
    /// <param name="latitudeSegments">Количество сегментов по широте (от полюса до полюса)</param>
    /// <param name="longitudeSegments">Количество сегментов по долготе (вокруг экватора)</param>
    /// <returns>Меш поверхности пористой сферы</returns>
    public Mesh BuildSurfaceMesh(int latitudeSegments, int longitudeSegments)
    {
        latitudeSegments = Mathf.Max(2, latitudeSegments);
        longitudeSegments = Mathf.Max(3, longitudeSegments);

        int vertexCount = (latitudeSegments + 1) * (longitudeSegments + 1);
        var vertices = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];

        // Генерируем вершины
        for (int lat = 0; lat <= latitudeSegments; lat++)
        {
            float theta = Mathf.PI * lat / latitudeSegments; // 0..PI (от северного полюса к южному)
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);

            for (int lon = 0; lon <= longitudeSegments; lon++)
            {
                float phi = 2f * Mathf.PI * lon / longitudeSegments; // 0..2PI
                float cosPhi = Mathf.Cos(phi);
                float sinPhi = Mathf.Sin(phi);

                // Направление на единичной сфере
                Vector3 direction = new Vector3(
                    sinTheta * cosPhi,
                    cosTheta,
                    sinTheta * sinPhi
                ).normalized;

                // Вычисляем радиус поверхности используя тот же метод, что и констрейн
                float radius = GetAllowedRadius(direction);

                // Позиция вершины на поверхности
                int vertexIndex = lat * (longitudeSegments + 1) + lon;
                vertices[vertexIndex] = sphereCenter + direction * radius;

                // Нормаль - направление от центра (для правильного освещения)
                normals[vertexIndex] = direction;

                // UV координаты
                uvs[vertexIndex] = new Vector2(
                    (float)lon / longitudeSegments,
                    (float)lat / latitudeSegments
                );
            }
        }

        // Строим треугольники
        int triangleCount = latitudeSegments * longitudeSegments * 2;
        var triangles = new int[triangleCount * 3];
        int triangleIndex = 0;

        for (int lat = 0; lat < latitudeSegments; lat++)
        {
            for (int lon = 0; lon < longitudeSegments; lon++)
            {
                int current = lat * (longitudeSegments + 1) + lon;
                int next = current + longitudeSegments + 1;

                // Первый треугольник квадранта
                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current + 1;

                // Второй треугольник квадранта
                triangles[triangleIndex++] = current + 1;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = next + 1;
            }
        }

        // Создаем меш
        var mesh = new Mesh();
        mesh.name = "PorousSphereSurface";
        
        if (vertexCount > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
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

        Vector3 toNext = nextPos - sphereCenter;
        float currentRadius = toNext.magnitude;

        if (currentRadius < 1e-6f)
        {
            // В центре - используем направление по умолчанию
            Vector3 dir = direction.magnitude > 1e-6f ? direction.normalized : Vector3.up;
            float allowedRad = GetAllowedRadius(dir);
            result.newPosition = sphereCenter + dir * allowedRad;
            return result;
        }

        Vector3 directionNormalized = toNext / currentRadius;
        float allowedRadius = GetAllowedRadius(directionNormalized);

        if (currentRadius > allowedRadius + 1e-6f)
        {
            // Мы вышли за пористую поверхность
            result.wasViolated = true;

            if (outMode == OutMode.StopBranch)
            {
                result.shouldStop = true;
                return result;
            }

            // Проецируем на поверхность
            result.newPosition = sphereCenter + directionNormalized * allowedRadius;

            if (outMode == OutMode.ClampToSurfaceStop)
            {
                result.shouldStop = true;
                return result;
            }

            // ClampToSurfaceAndSlide: меняем направление на касательное к поверхности
            result.newDirection = SlideDirectionOnPorousSurface(direction, result.newPosition, directionNormalized);
        }

        return result;
    }

    /// <summary>
    /// Вычисляет касательное направление для скольжения вдоль пористой поверхности.
    /// </summary>
    private Vector3 SlideDirectionOnPorousSurface(Vector3 dir, Vector3 posOnSurface, Vector3 surfaceNormal)
    {
        // Нормаль к поверхности (радиальная)
        Vector3 n = SafeNormalize(surfaceNormal, Vector3.up);

        // Убираем радиальную компоненту из направления
        Vector3 tangent = dir - Vector3.Dot(dir, n) * n;
        tangent = SafeNormalize(tangent, AnyPerpendicular(n));

        // Смешиваем: 0 = не трогать, 1 = полностью касательная
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
