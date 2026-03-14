using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using GlanceSearch.Shared;
using GlanceSearch.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GlanceSearch.Infrastructure.Persistence;

/// <summary>
/// Service for managing capture history: save, search, delete, purge.
/// Stores captured images as compressed JPEGs + thumbnails.
/// </summary>
public class HistoryService
{
    private readonly string _dbPath;
    private readonly string _capturesPath;

    public HistoryService()
    {
        _dbPath = Constants.HistoryDbPath;
        _capturesPath = Constants.CapturesPath;
        Directory.CreateDirectory(_capturesPath);

        // Ensure database is created
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    private HistoryDbContext CreateContext() => new(_dbPath);

    /// <summary>
    /// Save a capture result to history.
    /// </summary>
    public async Task<string> SaveCaptureAsync(
        Bitmap? croppedImage,
        OcrResult ocrResult,
        ContentDetectionResult detection,
        Rectangle selectionBounds)
    {
        var id = Guid.NewGuid().ToString();

        try
        {
            // Save image and thumbnail
            var imagePath = "";
            var thumbPath = "";

            if (croppedImage != null)
            {
                imagePath = Path.Combine(_capturesPath, $"{id}.jpg");
                thumbPath = Path.Combine(_capturesPath, $"{id}_thumb.jpg");

                // Save full image as JPEG (quality 85)
                var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                    .First(e => e.FormatID == ImageFormat.Jpeg.Guid);
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality, 85L);
                croppedImage.Save(imagePath, jpegEncoder, encoderParams);

                // Save thumbnail (max 200px wide)
                SaveThumbnail(croppedImage, thumbPath, 200);
            }

            // Create entity
            var entity = new CaptureHistoryEntity
            {
                Id = id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SelectionImagePath = imagePath,
                ThumbnailPath = thumbPath,
                SelectionX = selectionBounds.X,
                SelectionY = selectionBounds.Y,
                SelectionWidth = selectionBounds.Width,
                SelectionHeight = selectionBounds.Height,
                ExtractedText = ocrResult.ExtractedText,
                OcrLanguage = ocrResult.DetectedLanguage,
                OcrConfidence = ocrResult.Confidence,
                OcrEngine = ocrResult.EngineUsed,
                DetectedContentTypes = JsonSerializer.Serialize(
                    detection.DetectedTypes.Select(t => t.ToString())),
                DetectedUrls = detection.Urls.Count > 0
                    ? JsonSerializer.Serialize(detection.Urls) : null,
                DetectedEmails = detection.Emails.Count > 0
                    ? JsonSerializer.Serialize(detection.Emails) : null,
                DetectedPhones = detection.PhoneNumbers.Count > 0
                    ? JsonSerializer.Serialize(detection.PhoneNumbers) : null,
                QrBarcodeContent = detection.QrBarcodeContent,
            };

            // Save to database
            await using var db = CreateContext();
            db.CaptureHistory.Add(entity);
            await db.SaveChangesAsync();

            Log.Information("Saved capture {Id} to history", id);
            return id;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save capture to history");
            return id;
        }
    }

    /// <summary>
    /// Get the most recent history items.
    /// </summary>
    public async Task<List<CaptureHistoryEntity>> GetRecentAsync(int count = 50, int offset = 0)
    {
        await using var db = CreateContext();
        return await db.CaptureHistory
            .OrderByDescending(h => h.CreatedAt)
            .Skip(offset)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Search history by extracted text.
    /// </summary>
    public async Task<List<CaptureHistoryEntity>> SearchAsync(string query, int count = 50)
    {
        if (string.IsNullOrWhiteSpace(query)) return await GetRecentAsync(count);

        await using var db = CreateContext();
        var lowerQuery = query.ToLowerInvariant();
        return await db.CaptureHistory
            .Where(h => h.ExtractedText != null &&
                        EF.Functions.Like(h.ExtractedText, $"%{lowerQuery}%"))
            .OrderByDescending(h => h.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Get only pinned items.
    /// </summary>
    public async Task<List<CaptureHistoryEntity>> GetPinnedAsync()
    {
        await using var db = CreateContext();
        return await db.CaptureHistory
            .Where(h => h.IsPinned)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Toggle pin status.
    /// </summary>
    public async Task TogglePinAsync(string id)
    {
        await using var db = CreateContext();
        var item = await db.CaptureHistory.FindAsync(id);
        if (item != null)
        {
            item.IsPinned = !item.IsPinned;
            item.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Delete a history item and its associated images.
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        await using var db = CreateContext();
        var item = await db.CaptureHistory.FindAsync(id);
        if (item != null)
        {
            // Delete image files
            TryDeleteFile(item.SelectionImagePath);
            TryDeleteFile(item.ThumbnailPath);

            db.CaptureHistory.Remove(item);
            await db.SaveChangesAsync();
            Log.Debug("Deleted history item {Id}", id);
        }
    }

    /// <summary>
    /// Delete all history and images.
    /// </summary>
    public async Task ClearAllAsync()
    {
        await using var db = CreateContext();
        db.CaptureHistory.RemoveRange(db.CaptureHistory);
        await db.SaveChangesAsync();

        // Delete all capture images
        if (Directory.Exists(_capturesPath))
        {
            foreach (var file in Directory.GetFiles(_capturesPath))
                TryDeleteFile(file);
        }

        Log.Information("Cleared all history");
    }

    /// <summary>
    /// Get total item count.
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        await using var db = CreateContext();
        return await db.CaptureHistory.CountAsync();
    }

    /// <summary>
    /// Get total storage size in bytes.
    /// </summary>
    public long GetStorageSize()
    {
        if (!Directory.Exists(_capturesPath)) return 0;
        return Directory.GetFiles(_capturesPath)
            .Sum(f => new FileInfo(f).Length);
    }

    /// <summary>
    /// Auto-purge items beyond retention/max limits per user settings.
    /// </summary>
    public async Task PurgeAsync(int retentionDays, int maxItems)
    {
        await using var db = CreateContext();

        // Delete items older than retention
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var oldItems = await db.CaptureHistory
            .Where(h => h.CreatedAt < cutoff && !h.IsPinned)
            .ToListAsync();

        foreach (var item in oldItems)
        {
            TryDeleteFile(item.SelectionImagePath);
            TryDeleteFile(item.ThumbnailPath);
        }
        db.CaptureHistory.RemoveRange(oldItems);

        // Enforce max items (keep newest, remove oldest non-pinned)
        var totalCount = await db.CaptureHistory.CountAsync();
        if (totalCount > maxItems)
        {
            var excess = await db.CaptureHistory
                .Where(h => !h.IsPinned)
                .OrderBy(h => h.CreatedAt)
                .Take(totalCount - maxItems)
                .ToListAsync();

            foreach (var item in excess)
            {
                TryDeleteFile(item.SelectionImagePath);
                TryDeleteFile(item.ThumbnailPath);
            }
            db.CaptureHistory.RemoveRange(excess);
        }

        await db.SaveChangesAsync();
        Log.Debug("Purge: removed {Old} expired + checked max items", oldItems.Count);
    }

    #region Helpers

    private static void SaveThumbnail(Bitmap source, string path, int maxWidth)
    {
        var ratio = (double)maxWidth / source.Width;
        if (ratio >= 1) ratio = 1;

        var newWidth = (int)(source.Width * ratio);
        var newHeight = (int)(source.Height * ratio);

        using var thumb = new Bitmap(newWidth, newHeight);
        using (var g = Graphics.FromImage(thumb))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(source, 0, 0, newWidth, newHeight);
        }

        var jpegEncoder = ImageCodecInfo.GetImageEncoders()
            .First(e => e.FormatID == ImageFormat.Jpeg.Guid);
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(
            System.Drawing.Imaging.Encoder.Quality, 70L);
        thumb.Save(path, jpegEncoder, encoderParams);
    }

    private static void TryDeleteFile(string? path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try { File.Delete(path); }
            catch { /* ignore */ }
        }
    }

    #endregion
}
