using System;
using System.Collections.Generic;
using UnityEngine;

namespace PicShow
{
    public readonly struct WallGridRect
    {
        public readonly Vector2 Center;
        public readonly Vector2 Size;

        public WallGridRect(Vector2 center, Vector2 size)
        {
            Center = center;
            Size = size;
        }
    }

    public readonly struct WallGridResult
    {
        public readonly WallGridRect[] Rects;
        public readonly int Rows;
        public readonly int Columns;

        public WallGridResult(WallGridRect[] rects, int rows, int columns)
        {
            Rects = rects;
            Rows = rows;
            Columns = columns;
        }
    }

    public static class WallGridLayout
    {
        public static WallGridResult Compute(Vector2 wallSize, IReadOnlyList<float> aspectRatios,
            float marginRatio, float gapRatio)
        {
            if (aspectRatios == null || aspectRatios.Count == 0)
                throw new ArgumentException("At least one image aspect ratio is required.");
            if (wallSize.x <= 0f || wallSize.y <= 0f)
                throw new ArgumentOutOfRangeException(nameof(wallSize));

            int count = aspectRatios.Count;
            WallGridRect[] best = null;
            int bestRows = 1;
            int bestColumns = count;
            float bestScore = float.NegativeInfinity;

            for (int rows = 1; rows <= count; rows++)
            {
                int columns = (count + rows - 1) / rows;
                WallGridRect[] candidate = Build(wallSize, aspectRatios, marginRatio, gapRatio,
                    rows, columns, out float score);
                if (score > bestScore + 0.0001f)
                {
                    best = candidate;
                    bestScore = score;
                    bestRows = rows;
                    bestColumns = columns;
                }
            }

            return new WallGridResult(best, bestRows, bestColumns);
        }

        private static WallGridRect[] Build(Vector2 wallSize, IReadOnlyList<float> ratios,
            float marginRatio, float gapRatio, int rows, int columns, out float score)
        {
            float marginX = wallSize.x * Mathf.Max(0f, marginRatio);
            float marginY = wallSize.y * Mathf.Max(0f, marginRatio);
            float gap = Mathf.Min(wallSize.x, wallSize.y) * Mathf.Max(0f, gapRatio);
            float usableWidth = Mathf.Max(0.01f, wallSize.x - marginX * 2f);
            float usableHeight = Mathf.Max(0.01f, wallSize.y - marginY * 2f);
            float cellWidth = Mathf.Max(0.01f,
                (usableWidth - gap * (columns - 1)) / columns);
            float cellHeight = Mathf.Max(0.01f,
                (usableHeight - gap * (rows - 1)) / rows);

            var rects = new WallGridRect[ratios.Count];
            score = float.PositiveInfinity;
            int index = 0;
            for (int row = 0; row < rows; row++)
            {
                int rowCount = RowCount(ratios.Count, rows, row);
                for (int column = 0; column < rowCount; column++)
                {
                    float aspect = Mathf.Max(0.01f, ratios[index]);
                    Vector2 size = Fit(aspect, cellWidth, cellHeight);
                    score = Mathf.Min(score, size.x * size.y);

                    float rowWidth = rowCount * cellWidth + (rowCount - 1) * gap;
                    float x = -rowWidth / 2f + cellWidth / 2f + column * (cellWidth + gap);
                    float rowHeight = rows * cellHeight + (rows - 1) * gap;
                    float topY = rowHeight / 2f;
                    float y = topY - cellHeight / 2f - row * (cellHeight + gap);
                    rects[index] = new WallGridRect(new Vector2(x, y), size);
                    index++;
                }
            }

            return rects;
        }

        private static int RowCount(int total, int rows, int row)
        {
            int baseCount = total / rows;
            int extra = total % rows;
            return baseCount + (row < extra ? 1 : 0);
        }

        private static Vector2 Fit(float aspect, float cellWidth, float cellHeight)
        {
            float width = cellWidth;
            float height = width / aspect;
            if (height > cellHeight)
            {
                height = cellHeight;
                width = height * aspect;
            }
            return new Vector2(width, height);
        }
    }
}
