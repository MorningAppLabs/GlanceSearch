using System.ComponentModel.DataAnnotations;

namespace GlanceSearch.Infrastructure.Persistence;

/// <summary>
/// EF Core entity for capture history records.
/// Matches the PRD's CaptureHistory SQLite schema.
/// </summary>
public class CaptureHistoryEntity
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Selection data
    public string SelectionImagePath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public int SelectionX { get; set; }
    public int SelectionY { get; set; }
    public int SelectionWidth { get; set; }
    public int SelectionHeight { get; set; }

    // OCR data
    public string? ExtractedText { get; set; }
    public string? OcrLanguage { get; set; }
    public double OcrConfidence { get; set; }
    public string OcrEngine { get; set; } = "Windows";

    // Content detection
    public string? DetectedContentTypes { get; set; } // JSON array
    public string? DetectedUrls { get; set; }
    public string? DetectedEmails { get; set; }
    public string? DetectedPhones { get; set; }
    public string? QrBarcodeContent { get; set; }

    // Context
    public string? SourceWindowTitle { get; set; }
    public string? SourceProcessName { get; set; }

    // User actions
    public string? ActionsTaken { get; set; } // JSON array
    public bool IsPinned { get; set; } = false;
}
