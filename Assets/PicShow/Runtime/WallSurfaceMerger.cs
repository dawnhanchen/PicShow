using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PicShow
{
    public readonly struct WallSurface
    {
        public readonly WallPlaneInfo Wall;
        public readonly IReadOnlyList<Guid> SourceIds;

        public WallSurface(WallPlaneInfo wall, IReadOnlyList<Guid> sourceIds)
        {
            Wall = wall;
            SourceIds = sourceIds;
        }

        public Guid Id => Wall.Id;
    }

    // PICO plane detection may split one real wall into several coplanar patches.
    // This merges adjacent patches into one wall surface so the UI creates one
    // selection button per physical wall instead of one per detected mesh.
    public sealed class WallSurfaceMerger
    {
        private const float MaxNormalAngleDegrees = 20f;
        private const float MaxPlaneDistanceMeters = 0.10f;
        private const float MaxPlanarGapMeters = 0.25f;

        private readonly Dictionary<Guid, WallPlaneInfo> patches =
            new Dictionary<Guid, WallPlaneInfo>();
        private readonly List<WallSurface> surfaces = new List<WallSurface>();

        public IReadOnlyList<WallSurface> Surfaces => surfaces;

        public void Clear()
        {
            patches.Clear();
            surfaces.Clear();
        }

        public void AddOrUpdate(WallPlaneInfo patch)
        {
            if (patch.Id == Guid.Empty)
                return;
            patches[patch.Id] = patch;
            Rebuild();
        }

        public bool Remove(Guid id)
        {
            if (!patches.Remove(id))
                return false;
            Rebuild();
            return true;
        }

        private void Rebuild()
        {
            if (patches.Count == 0)
            {
                surfaces.Clear();
                return;
            }

            List<WallPlaneInfo> values = patches.Values.ToList();
            int[] parent = Enumerable.Range(0, values.Count).ToArray();
            for (int i = 0; i < values.Count; i++)
            {
                for (int j = i + 1; j < values.Count; j++)
                {
                    if (ShouldMerge(values[i], values[j]))
                        Union(parent, i, j);
                }
            }

            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < values.Count; i++)
            {
                int root = Find(parent, i);
                if (!groups.TryGetValue(root, out List<int> group))
                {
                    group = new List<int>();
                    groups.Add(root, group);
                }
                group.Add(i);
            }

            List<WallSurface> previous = surfaces.ToList();
            var usedIds = new HashSet<Guid>();
            var next = new List<WallSurface>();
            foreach (List<int> group in groups.Values
                         .OrderByDescending(item => item.Count)
                         .ThenBy(item => values[item[0]].Id))
            {
                Guid[] sourceIds = group.Select(index => values[index].Id)
                    .OrderBy(id => id).ToArray();
                Guid id = PickId(previous, usedIds, sourceIds);
                usedIds.Add(id);
                next.Add(new WallSurface(Merge(values, group, id), sourceIds));
            }

            surfaces.Clear();
            surfaces.AddRange(next);
        }

        private static bool ShouldMerge(WallPlaneInfo first, WallPlaneInfo second)
        {
            float normalDot = Mathf.Abs(Vector3.Dot(first.Normal, second.Normal));
            if (normalDot < Mathf.Cos(MaxNormalAngleDegrees * Mathf.Deg2Rad))
                return false;
            float planeDistance = Mathf.Abs(
                Vector3.Dot(first.Normal, second.Center - first.Center));
            if (planeDistance > MaxPlaneDistanceMeters)
                return false;

            Rect firstRect = ProjectRect(first, first.Right, first.Up, first.Center);
            Rect secondRect = ProjectRect(second, first.Right, first.Up, first.Center);
            firstRect.xMin -= MaxPlanarGapMeters;
            firstRect.xMax += MaxPlanarGapMeters;
            firstRect.yMin -= MaxPlanarGapMeters;
            firstRect.yMax += MaxPlanarGapMeters;
            return firstRect.Overlaps(secondRect);
        }

        private static Rect ProjectRect(WallPlaneInfo wall, Vector3 axisRight, Vector3 axisUp,
            Vector3 origin)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int rightSign = -1; rightSign <= 1; rightSign += 2)
            {
                for (int upSign = -1; upSign <= 1; upSign += 2)
                {
                    Vector3 corner = wall.Center
                        + wall.Right * (rightSign * wall.Width * 0.5f)
                        + wall.Up * (upSign * wall.Height * 0.5f);
                    Vector3 offset = corner - origin;
                    Vector2 point = new Vector2(
                        Vector3.Dot(offset, axisRight), Vector3.Dot(offset, axisUp));
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static WallPlaneInfo Merge(List<WallPlaneInfo> values, List<int> group, Guid id)
        {
            Vector3 normalSum = Vector3.zero;
            foreach (int index in group)
                normalSum += values[index].Normal;
            Vector3 normal = normalSum.sqrMagnitude > 0.0001f
                ? normalSum.normalized : values[group[0]].Normal.normalized;

            Vector3 up = Vector3.ProjectOnPlane(values[group[0]].Up, normal);
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.ProjectOnPlane(Vector3.up, normal);
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.ProjectOnPlane(Vector3.forward, normal);
            up.Normalize();
            Vector3 right = Vector3.Cross(up, normal).normalized;
            up = Vector3.Cross(normal, right).normalized;

            Vector3 origin = values[group[0]].Center;
            float minRight = float.PositiveInfinity;
            float maxRight = float.NegativeInfinity;
            float minUp = float.PositiveInfinity;
            float maxUp = float.NegativeInfinity;
            foreach (int index in group)
            {
                WallPlaneInfo wall = values[index];
                for (int rightSign = -1; rightSign <= 1; rightSign += 2)
                {
                    for (int upSign = -1; upSign <= 1; upSign += 2)
                    {
                        Vector3 corner = wall.Center
                            + wall.Right * (rightSign * wall.Width * 0.5f)
                            + wall.Up * (upSign * wall.Height * 0.5f);
                        Vector3 offset = corner - origin;
                        float rightOffset = Vector3.Dot(offset, right);
                        float upOffset = Vector3.Dot(offset, up);
                        minRight = Mathf.Min(minRight, rightOffset);
                        maxRight = Mathf.Max(maxRight, rightOffset);
                        minUp = Mathf.Min(minUp, upOffset);
                        maxUp = Mathf.Max(maxUp, upOffset);
                    }
                }
            }

            Vector3 center = origin + right * ((minRight + maxRight) * 0.5f)
                + up * ((minUp + maxUp) * 0.5f);
            return new WallPlaneInfo(id, center, normal, right, up,
                maxRight - minRight, maxUp - minUp);
        }

        private static Guid PickId(List<WallSurface> previous, HashSet<Guid> usedIds,
            Guid[] sourceIds)
        {
            var ids = new HashSet<Guid>(sourceIds);
            Guid bestId = Guid.Empty;
            int bestOverlap = 0;
            foreach (WallSurface surface in previous)
            {
                if (usedIds.Contains(surface.Id))
                    continue;
                int overlap = 0;
                foreach (Guid sourceId in surface.SourceIds)
                {
                    if (ids.Contains(sourceId))
                        overlap++;
                }
                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    bestId = surface.Id;
                }
            }
            return bestOverlap > 0 ? bestId : PickUnusedFallback(sourceIds, usedIds);
        }

        private static Guid PickUnusedFallback(Guid[] sourceIds, HashSet<Guid> usedIds)
        {
            foreach (Guid sourceId in sourceIds)
            {
                if (!usedIds.Contains(sourceId))
                    return sourceId;
            }

            Guid baseId = sourceIds[0];
            for (int salt = 1; salt < 256; salt++)
            {
                byte[] bytes = baseId.ToByteArray();
                bytes[0] = (byte)(bytes[0] ^ salt);
                var candidate = new Guid(bytes);
                if (!usedIds.Contains(candidate))
                    return candidate;
            }
            return Guid.NewGuid();
        }

        private static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }
            return index;
        }

        private static void Union(int[] parent, int first, int second)
        {
            int firstRoot = Find(parent, first);
            int secondRoot = Find(parent, second);
            if (firstRoot != secondRoot)
                parent[secondRoot] = firstRoot;
        }
    }
}
