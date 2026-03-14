namespace GlanceSearch.Shared.Models;

/// <summary>
/// Result of running all content detectors on OCR output and/or the captured image.
/// </summary>
public class ContentDetectionResult
{
    /// <summary>Ordered list of detected content types (highest priority first).</summary>
    public List<ContentType> DetectedTypes { get; set; } = [];

    /// <summary>Detected URLs (http/https/www).</summary>
    public List<string> Urls { get; set; } = [];

    /// <summary>Detected email addresses.</summary>
    public List<string> Emails { get; set; } = [];

    /// <summary>Detected phone numbers.</summary>
    public List<string> PhoneNumbers { get; set; } = [];

    /// <summary>Decoded QR/barcode content (null if none found).</summary>
    public string? QrBarcodeContent { get; set; }

    /// <summary>Detected color values (hex, rgb, hsl strings).</summary>
    public List<string> Colors { get; set; } = [];

    /// <summary>Whether the text appears to be code.</summary>
    public bool IsCode { get; set; }

    /// <summary>True if at least one content type was detected beyond plain text.</summary>
    public bool HasSmartContent => DetectedTypes.Any(t =>
        t != ContentType.PlainText && t != ContentType.PureImage);
}
