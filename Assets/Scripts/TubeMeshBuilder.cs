// TubeMeshBuilder.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public static class TubeMeshBuilder
{
    public enum FrameMode { ParallelTransport, FixedUp }

    /// <param name="uByPoint01">
    /// Если передан, то должен быть длиной points.Count и содержать uv.x (0..1) для каждой точки пути.
    /// Это нужно для "глобального" роста по всему дереву.
    /// </param>
    public static Mesh Build(
        IReadOnlyList<Vector3> points,
        float radius,
        int sides = 8,
        bool capStart = false,
        bool capEnd = false,
        bool smoothNormals = true,
        FrameMode frameMode = FrameMode.ParallelTransport,
        Vector3 fixedUp = default,
        IReadOnlyList<float> uByPoint01 = null
    )
    {
        if (points == null) throw new ArgumentNullException(nameof(points));
        if (points.Count < 2) throw new ArgumentException("Need at least 2 points.", nameof(points));
        if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius), "radius must be > 0");
        if (sides < 3) throw new ArgumentOutOfRangeException(nameof(sides), "sides must be >= 3");
        if (uByPoint01 != null && uByPoint01.Count != points.Count)
            throw new ArgumentException("uByPoint01 must have the same length as points.", nameof(uByPoint01));

        if (fixedUp == default) fixedUp = Vector3.up;

        int n = points.Count;

        // Если глобальный U не дали — делаем локальный по длине ветки (как раньше)
        float[] localU = null;
        if (uByPoint01 == null)
        {
            localU = new float[n];
            float totalLen = 0f;
            localU[0] = 0f;
            for (int i = 1; i < n; i++)
            {
                totalLen += Vector3.Distance(points[i - 1], points[i]);
                localU[i] = totalLen;
            }
            float inv = totalLen > 1e-6f ? 1f / totalLen : 0f;
            for (int i = 0; i < n; i++) localU[i] *= inv;
        }

        // Тангенты пути
        var T = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 t;
            if (i == 0) t = (points[1] - points[0]);
            else if (i == n - 1) t = (points[n - 1] - points[n - 2]);
            else t = (points[i + 1] - points[i - 1]);
            T[i] = t.sqrMagnitude > 1e-12f ? t.normalized : Vector3.forward;
        }

        // Рамки (N,B)
        var N = new Vector3[n];
        var B = new Vector3[n];

        if (frameMode == FrameMode.FixedUp)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 up = fixedUp;
                if (Mathf.Abs(Vector3.Dot(up.normalized, T[i])) > 0.98f)
                    up = Mathf.Abs(Vector3.Dot(Vector3.right, T[i])) < 0.98f ? Vector3.right : Vector3.forward;

                Quaternion q = Quaternion.LookRotation(T[i], up);
                B[i] = (q * Vector3.right).normalized;
                N[i] = Vector3.Cross(T[i], B[i]).normalized;
            }
        }
        else
        {
            Vector3 n0 = Vector3.Cross(T[0], fixedUp);
            if (n0.sqrMagnitude < 1e-8f) n0 = Vector3.Cross(T[0], Vector3.right);
            if (n0.sqrMagnitude < 1e-8f) n0 = Vector3.Cross(T[0], Vector3.forward);

            N[0] = n0.normalized;
            B[0] = Vector3.Cross(T[0], N[0]).normalized;

            for (int i = 1; i < n; i++)
            {
                Vector3 v = Vector3.Cross(T[i - 1], T[i]);
                float vLen = v.magnitude;

                if (vLen < 1e-6f)
                {
                    N[i] = N[i - 1];
                    B[i] = B[i - 1];
                }
                else
                {
                    Vector3 axis = v / vLen;
                    float angle = Mathf.Atan2(vLen, Vector3.Dot(T[i - 1], T[i])) * Mathf.Rad2Deg;
                    Quaternion rot = Quaternion.AngleAxis(angle, axis);

                    N[i] = (rot * N[i - 1]).normalized;
                    B[i] = Vector3.Cross(T[i], N[i]).normalized;
                    N[i] = Vector3.Cross(B[i], T[i]).normalized;
                }
            }
        }

        int ringVerts = sides;
        int rings = n;
        int bodyVertCount = rings * ringVerts;

        int capStartVertCount = capStart ? (ringVerts + 1) : 0;
        int capEndVertCount = capEnd ? (ringVerts + 1) : 0;

        int vertCount = bodyVertCount + capStartVertCount + capEndVertCount;

        var vertices = new Vector3[vertCount];
        var normals = new Vector3[vertCount];
        var uvs = new Vector2[vertCount];
        var tangents = new Vector4[vertCount];

        int bodyTriCount = (rings - 1) * sides * 2;
        int capTriCount = (capStart ? sides : 0) + (capEnd ? sides : 0);
        var indices = new int[(bodyTriCount + capTriCount) * 3];

        var circle = new Vector2[sides];
        for (int s = 0; s < sides; s++)
        {
            float a = (s / (float)sides) * Mathf.PI * 2f;
            circle[s] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        int vtx = 0;

        for (int i = 0; i < rings; i++)
        {
            float u = (uByPoint01 != null) ? Mathf.Clamp01(uByPoint01[i]) : localU[i];

            for (int s = 0; s < sides; s++)
            {
                Vector2 c = circle[s];
                Vector3 radial = (B[i] * c.x + N[i] * c.y).normalized;

                vertices[vtx] = points[i] + radial * radius;
                normals[vtx] = smoothNormals ? radial : radial;

                float v = s / (float)sides;
                uvs[vtx] = new Vector2(u, v);

                Vector3 t3 = Vector3.Cross(normals[vtx], T[i]).normalized;
                tangents[vtx] = new Vector4(t3.x, t3.y, t3.z, 1f);

                vtx++;
            }
        }

        int idx = 0;
        for (int i = 0; i < rings - 1; i++)
        {
            int ring0 = i * ringVerts;
            int ring1 = (i + 1) * ringVerts;

            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;

                int a = ring0 + s;
                int b = ring1 + s;
                int c = ring1 + s1;
                int d = ring0 + s1;

                indices[idx++] = a; indices[idx++] = b; indices[idx++] = c;
                indices[idx++] = a; indices[idx++] = c; indices[idx++] = d;
            }
        }

        int vBase = bodyVertCount;

        if (capStart)
        {
            int capBase = vBase;
            int center = capBase + ringVerts;

            float u0 = (uByPoint01 != null) ? Mathf.Clamp01(uByPoint01[0]) : 0f;

            for (int s = 0; s < sides; s++)
            {
                int src = s;
                vertices[capBase + s] = vertices[src];
                normals[capBase + s] = -T[0];
                uvs[capBase + s] = new Vector2(u0, s / (float)sides);
                tangents[capBase + s] = tangents[src];
            }

            vertices[center] = points[0];
            normals[center] = -T[0];
            uvs[center] = new Vector2(u0, 0.5f);
            tangents[center] = new Vector4(1, 0, 0, 1);

            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                indices[idx++] = center;
                indices[idx++] = capBase + s1;
                indices[idx++] = capBase + s;
            }

            vBase += (ringVerts + 1);
        }

        if (capEnd)
        {
            int capBase = vBase;
            int center = capBase + ringVerts;

            int lastRing = (rings - 1) * ringVerts;
            float u1 = (uByPoint01 != null) ? Mathf.Clamp01(uByPoint01[rings - 1]) : 1f;

            for (int s = 0; s < sides; s++)
            {
                int src = lastRing + s;
                vertices[capBase + s] = vertices[src];
                normals[capBase + s] = T[rings - 1];
                uvs[capBase + s] = new Vector2(u1, s / (float)sides);
                tangents[capBase + s] = tangents[src];
            }

            vertices[center] = points[rings - 1];
            normals[center] = T[rings - 1];
            uvs[center] = new Vector2(u1, 0.5f);
            tangents[center] = new Vector4(1, 0, 0, 1);

            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                indices[idx++] = center;
                indices[idx++] = capBase + s;
                indices[idx++] = capBase + s1;
            }
        }

        var mesh = new Mesh();
        mesh.name = "TubeMesh";
        if (vertCount > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.tangents = tangents;
        mesh.uv = uvs;
        mesh.triangles = indices;
        mesh.RecalculateBounds();
        return mesh;
    }
}
