using System.Drawing;
using GlanceSearch.Shared;

namespace GlanceSearch.Core.Selection;

/// <summary>
/// Processes raw selection points into smoothed paths and crops the selected region.
/// </summary>
public class SelectionService
{
    /// <summary>
    /// Smooth a freehand selection path using Catmull-Rom spline interpolation.
    /// </summary>
    public List<Point> SmoothPath(List<Point> rawPoints, int subdivisions = 10)
    {
        if (rawPoints.Count < 4)
            return rawPoints;

        var smoothed = new List<Point>();

        for (int i = 0; i < rawPoints.Count - 1; i++)
        {
            var p0 = rawPoints[Math.Max(i - 1, 0)];
            var p1 = rawPoints[i];
            var p2 = rawPoints[Math.Min(i + 1, rawPoints.Count - 1)];
            var p3 = rawPoints[Math.Min(i + 2, rawPoints.Count - 1)];

            for (int j = 0; j < subdivisions; j++)
            {
                float t = j / (float)subdivisions;
                var point = CatmullRom(p0, p1, p2, p3, t);
                smoothed.Add(point);
            }
        }

        // Add the last point
        smoothed.Add(rawPoints[^1]);

        return smoothed;
    }

    /// <summary>
    /// Catmull-Rom spline interpolation between 4 control points.
    /// </summary>
    private Point CatmullRom(Point p0, Point p1, Point p2, Point p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float x = 0.5f * (
            (2 * p1.X) +
            (-p0.X + p2.X) * t +
            (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 +
            (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3
        );

        float y = 0.5f * (
            (2 * p1.Y) +
            (-p0.Y + p2.Y) * t +
            (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 +
            (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3
        );

        return new Point((int)x, (int)y);
    }

    /// <summary>
    /// Get the bounding rectangle of a set of points.
    /// </summary>
    public Rectangle GetBoundingBox(List<Point> points)
    {
        if (points.Count == 0)
            return Rectangle.Empty;

        int minX = points.Min(p => p.X);
        int minY = points.Min(p => p.Y);
        int maxX = points.Max(p => p.X);
        int maxY = points.Max(p => p.Y);

        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Validates that the selection meets minimum size requirements.
    /// </summary>
    public bool IsValidSelection(Rectangle bounds)
    {
        return bounds.Width >= Constants.MinSelectionSize && bounds.Height >= Constants.MinSelectionSize;
    }

    /// <summary>
    /// Crop a region from the source bitmap.
    /// </summary>
    public Bitmap CropSelection(Bitmap source, Rectangle bounds)
    {
        // Clamp bounds to image
        var clampedBounds = Rectangle.Intersect(bounds, new Rectangle(0, 0, source.Width, source.Height));
        if (clampedBounds.Width <= 0 || clampedBounds.Height <= 0)
            return new Bitmap(1, 1);

        var cropped = new Bitmap(clampedBounds.Width, clampedBounds.Height);
        using var g = Graphics.FromImage(cropped);
        g.DrawImage(source, 0, 0, clampedBounds, GraphicsUnit.Pixel);
        return cropped;
    }
}
