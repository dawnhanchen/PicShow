using System;
using System.Collections.Generic;
using UnityEngine;

namespace PicShow
{
    public readonly struct WallPlaneInfo
    {
        public readonly Guid Id;
        public readonly Vector3 Center;
        public readonly Vector3 Normal;
        public readonly Vector3 Right;
        public readonly Vector3 Up;
        public readonly Quaternion Rotation;
        public readonly float Width;
        public readonly float Height;

        public WallPlaneInfo(Guid id, Vector3 center, Vector3 normal, Vector3 right, Vector3 up,
            float width, float height)
        {
            Id = id;
            Center = center;
            Normal = normal;
            Right = right;
            Up = up;
            Rotation = Quaternion.LookRotation(-normal, up);
            Width = width;
            Height = height;
        }
    }

    public static class WallGeometry
    {
        private const float VerticalNormalLimit = 0.2f;
        private const float MinimumWallSize = 0.05f;

        public static bool IsWall(bool semanticWall, Vector3 normal,
            bool allowGeometricFallback = false)
        {
            if (semanticWall)
                return true;
            if (!allowGeometricFallback)
                return false;
            if (normal.sqrMagnitude < 0.0001f)
                return false;
            return Mathf.Abs(normal.normalized.y) <= VerticalNormalLimit;
        }

        public static bool TryBuild(Guid id, IReadOnlyList<Vector3> vertices,
            IReadOnlyList<int> triangles, bool semanticWall, Vector3 cameraPosition,
            out WallPlaneInfo wall, out string error)
        {
            return TryBuild(id, vertices, triangles, semanticWall, cameraPosition, false,
                out wall, out error);
        }

        public static bool TryBuild(Guid id, IReadOnlyList<Vector3> vertices,
            IReadOnlyList<int> triangles, bool semanticWall, Vector3 cameraPosition,
            bool allowGeometricFallback, out WallPlaneInfo wall, out string error)
        {
            wall = default;
            error = null;
            if (vertices == null || vertices.Count < 3)
            {
                error = "A wall plane needs at least three vertices.";
                return false;
            }

            Vector3 normal = AverageNormal(vertices, triangles);
            if (!IsWall(semanticWall, normal, allowGeometricFallback))
            {
                error = "The plane is not vertical and has no Wall semantic label.";
                return false;
            }

            Vector3 origin = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
                origin += vertices[i];
            origin /= vertices.Count;

            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal);
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.ProjectOnPlane(Vector3.forward, normal);
            up.Normalize();
            Vector3 right = Vector3.Cross(up, normal).normalized;
            up = Vector3.Cross(normal, right).normalized;

            float minRight = float.PositiveInfinity;
            float maxRight = float.NegativeInfinity;
            float minUp = float.PositiveInfinity;
            float maxUp = float.NegativeInfinity;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 offset = vertices[i] - origin;
                float rightOffset = Vector3.Dot(offset, right);
                float upOffset = Vector3.Dot(offset, up);
                minRight = Mathf.Min(minRight, rightOffset);
                maxRight = Mathf.Max(maxRight, rightOffset);
                minUp = Mathf.Min(minUp, upOffset);
                maxUp = Mathf.Max(maxUp, upOffset);
            }

            float width = maxRight - minRight;
            float height = maxUp - minUp;
            if (width < MinimumWallSize || height < MinimumWallSize)
            {
                error = "The wall plane is too small to hold a UI.";
                return false;
            }

            Vector3 center = origin + right * ((minRight + maxRight) / 2f)
                + up * ((minUp + maxUp) / 2f);
            if (Vector3.Dot(normal, cameraPosition - center) < 0f)
            {
                normal = -normal;
                right = -right;
            }

            wall = new WallPlaneInfo(id, center, normal, right, up, width, height);
            return true;
        }

        private static Vector3 AverageNormal(IReadOnlyList<Vector3> vertices,
            IReadOnlyList<int> triangles)
        {
            Vector3 total = Vector3.zero;
            if (triangles != null && triangles.Count >= 3)
            {
                for (int i = 0; i + 2 < triangles.Count; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];
                    total += Vector3.Cross(b - a, c - a);
                }
            }
            else
            {
                for (int i = 1; i + 1 < vertices.Count; i++)
                    total += Vector3.Cross(vertices[i] - vertices[0], vertices[i + 1] - vertices[0]);
            }

            return total.sqrMagnitude < 0.0001f ? Vector3.zero : total.normalized;
        }
    }
}
