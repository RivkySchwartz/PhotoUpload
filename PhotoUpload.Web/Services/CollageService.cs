using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PhotoUpload.Web.Services;

public class CollageService : ICollageService
{
    private const int    CanvasWidth  = 2800;
    private const int    Gap          = 10;
    private const int    Padding      = 20;
    private const string WatermarkText = "Luminate Photography";

    private readonly IFileStorageService _storage;
    private readonly ILogger<CollageService> _logger;

    public CollageService(IFileStorageService storage, ILogger<CollageService> logger)
    {
        _storage = storage;
        _logger  = logger;
    }

    public async Task<string?> GenerateCollageAsync(
        IReadOnlyList<string> imageAbsolutePathsIn, string galleryToken)
    {
        if (imageAbsolutePathsIn.Count == 0) return null;

        try
        {
            // When more than 7 photos are selected, pick a random 5-7 for the collage
            IReadOnlyList<string> imageAbsolutePaths;
            if (imageAbsolutePathsIn.Count > 7)
            {
                var rng  = new Random();
                int pick = rng.Next(5, 8); // 5, 6, or 7
                imageAbsolutePaths = imageAbsolutePathsIn
                    .OrderBy(_ => rng.Next())
                    .Take(pick)
                    .ToList();
            }
            else
            {
                imageAbsolutePaths = imageAbsolutePathsIn;
            }

            // ── Build row layout ──────────────────────────────────────────────
            // Each row has 1-4 photos with varying widths and heights.
            // Width ratios for rows of different sizes give the magazine look.
            var rowDefs = BuildRowDefinitions(imageAbsolutePaths.Count);

            int innerW = CanvasWidth - Padding * 2;

            // Calculate each row's pixel height and photo rects
            var rowRects = new List<(int photoIdx, int x, int y, int w, int h)>();
            int curY = Padding;

            int photoIdx = 0;
            foreach (var (rowCounts, heightRatio) in rowDefs)
            {
                if (photoIdx >= imageAbsolutePaths.Count) break;

                int rowH = (int)(innerW * heightRatio);

                // Widths proportional to given ratios
                float totalRatio = rowCounts.Sum(r => r.widthRatio);
                int usedGap = (rowCounts.Count - 1) * Gap;

                int curX = Padding;
                foreach (var (widthRatio, _) in rowCounts)
                {
                    if (photoIdx >= imageAbsolutePaths.Count) break;
                    int cellW = (int)((widthRatio / totalRatio) * (innerW - usedGap));
                    rowRects.Add((photoIdx, curX, curY, cellW, rowH));
                    curX += cellW + Gap;
                    photoIdx++;
                }

                curY += rowH + Gap;
            }

            int totalH = curY - Gap + Padding;

            // ── Draw canvas ───────────────────────────────────────────────────
            using var canvas = new Image<Rgba32>(CanvasWidth, totalH, new Rgba32(10, 10, 10));

            foreach (var (idx, x, y, w, h) in rowRects)
            {
                var path = imageAbsolutePaths[idx];
                if (!System.IO.File.Exists(path)) continue;
                try
                {
                    using var cell = await Image.LoadAsync<Rgba32>(path);
                    // Canvas compositing has no browser to apply EXIF orientation for it —
                    // the pixels must be physically upright before drawing.
                    cell.Mutate(ctx => ctx.AutoOrient());
                    cell.Mutate(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size     = new Size(w, h),
                        Mode     = ResizeMode.Crop,
                        Position = AnchorPositionMode.Center
                    }));
                    canvas.Mutate(ctx => ctx.DrawImage(cell, new Point(x, y), 1f));
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Skipping {Path}", path); }
            }

            // ── Save ──────────────────────────────────────────────────────────
            var name    = $"collage_{DateTime.UtcNow.Ticks}.jpg";
            var relPath = $"{galleryToken}/collages/{name}";
            var absPath = _storage.GetAbsolutePath(relPath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(absPath)!);
            await canvas.SaveAsJpegAsync(absPath, new JpegEncoder { Quality = 95 });
            return relPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Collage failed for {Token}", galleryToken);
            return null;
        }
    }

    // ── Row layout engine ─────────────────────────────────────────────────────
    // Returns a sequence of rows. Each row = list of (widthRatio, placeholder) + heightRatio.
    // heightRatio is relative to the inner canvas width.

    private static List<(List<(float widthRatio, int _)> cells, float heightRatio)>
        BuildRowDefinitions(int photoCount)
    {
        // Define the repeating row pattern (cycles through these)
        var patterns = new List<(float[] widths, float height)>
        {
            (new[] { 1f },              0.40f),  // single hero — wide strip
            (new[] { 0.60f, 0.40f },   0.28f),  // two photos, unequal
            (new[] { 0.33f, 0.34f, 0.33f }, 0.24f), // three equal
            (new[] { 0.40f, 0.35f, 0.25f }, 0.26f), // three unequal
            (new[] { 0.50f, 0.50f },   0.26f),  // two equal
            (new[] { 0.25f, 0.50f, 0.25f }, 0.28f), // three — center hero
            (new[] { 0.25f, 0.25f, 0.25f, 0.25f }, 0.22f), // four equal
        };

        var rows = new List<(List<(float, int)>, float)>();
        int placed = 0;
        int patternIdx = 0;

        while (placed < photoCount)
        {
            var (widths, height) = patterns[patternIdx % patterns.Count];
            patternIdx++;

            int take = Math.Min(widths.Length, photoCount - placed);
            var cells = widths.Take(take).Select(w => (w, 0)).ToList();

            // Re-normalise widths if we took fewer than the full pattern
            rows.Add((cells, height));
            placed += take;
        }

        return rows;
    }

    // ── Diagonal watermark ────────────────────────────────────────────────────

    private static void DrawDiagonalWatermark(Image<Rgba32> canvas, int totalH)
    {
        // Find font size so text roughly spans the canvas diagonal
        float diagonal = MathF.Sqrt(CanvasWidth * CanvasWidth + (float)(totalH * totalH));
        float angle    = -MathF.Atan2(totalH, CanvasWidth); // exact diagonal angle

        // Start with a guess and measure
        float fontSize = 120f;
        var font = ResolveFont(fontSize);
        if (font == null) return;

        // Scale font so measured text width ≈ diagonal
        var measured = TextMeasurer.MeasureBounds(WatermarkText, new TextOptions(font));
        if (measured.Width > 0)
        {
            fontSize *= diagonal / measured.Width * 1.05f; // 5% overshoot so it bleeds edge-to-edge
            font = ResolveFont(fontSize);
            if (font == null) return;
        }

        float cx = CanvasWidth / 2f;
        float cy = totalH / 2f;

        var glyphs = TextBuilder.GenerateGlyphs(
            WatermarkText,
            new TextOptions(font)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Origin = new PointF(cx, cy),
            });

        var rotated = glyphs.Transform(
            Matrix3x2.CreateRotation(angle, new Vector2(cx, cy)));

        // Slightly transparent white so photos show through
        canvas.Mutate(ctx => ctx.Fill(new SolidBrush(new Rgba32(255, 255, 255, 65)), rotated));
    }

    private static Font? ResolveFont(float size)
    {
        string[] preferred = ["Segoe UI", "Arial", "Helvetica", "DejaVu Sans", "Liberation Sans"];
        foreach (var name in preferred)
        {
            try { return SystemFonts.CreateFont(name, size, FontStyle.Bold); }
            catch { }
        }
        try { return SystemFonts.Families.First().CreateFont(size, FontStyle.Bold); }
        catch { return null; }
    }
}
