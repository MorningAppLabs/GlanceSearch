using GlanceSearch.Core.ContentDetection.Detectors;
using GlanceSearch.Shared;
using GlanceSearch.Shared.Models;
using System.Diagnostics;

namespace GlanceSearch.Core.ContentDetection;

/// <summary>
/// Orchestrates all content detectors and produces a unified ContentDetectionResult.
/// Priority order: QR/Barcode → URL → Email → Phone → Code → Color → PlainText → PureImage.
/// </summary>
public class ContentClassifier
{
    /// <summary>
    /// Analyze OCR text and captured image to classify content.
    /// </summary>
    /// <param name="text">Extracted OCR text (may be empty).</param>
    /// <param name="capturedImage">The cropped bitmap from screen capture (may be null).</param>
    public ContentDetectionResult Classify(string? text, System.Drawing.Bitmap? capturedImage)
    {
        var result = new ContentDetectionResult();
        var hasText = !string.IsNullOrWhiteSpace(text);

        try
        {
            // 1. QR/Barcode detection (runs on image, not text)
            var qrContent = QrBarcodeDetector.Detect(capturedImage);
            if (!string.IsNullOrEmpty(qrContent))
            {
                result.QrBarcodeContent = qrContent;
                result.DetectedTypes.Add(ContentType.QrCode);
                Debug.WriteLine($"Detected QR/Barcode: {qrContent}");
            }

            if (hasText)
            {
                // 2. URLs
                var urls = UrlDetector.Detect(text!);
                if (urls.Count > 0)
                {
                    result.Urls = urls;
                    result.DetectedTypes.Add(ContentType.Url);
                    Debug.WriteLine($"Detected {urls.Count} URL(s)");
                }

                // 3. Emails
                var emails = EmailDetector.Detect(text!);
                if (emails.Count > 0)
                {
                    result.Emails = emails;
                    result.DetectedTypes.Add(ContentType.Email);
                    Debug.WriteLine($"Detected {emails.Count} email(s)");
                }

                // 4. Phone numbers
                var phones = PhoneDetector.Detect(text!);
                if (phones.Count > 0)
                {
                    result.PhoneNumbers = phones;
                    result.DetectedTypes.Add(ContentType.PhoneNumber);
                    Debug.WriteLine($"Detected {phones.Count} phone number(s)");
                }

                // 5. Code snippets
                var (isCode, confidence) = CodeDetector.Detect(text!);
                if (isCode)
                {
                    result.IsCode = true;
                    result.DetectedTypes.Add(ContentType.CodeSnippet);
                    Debug.WriteLine($"Detected code snippet (confidence: {confidence:F2})");
                }

                // 6. Color values
                var colors = ColorDetector.Detect(text!);
                if (colors.Count > 0)
                {
                    result.Colors = colors;
                    result.DetectedTypes.Add(ContentType.ColorValue);
                    Debug.WriteLine($"Detected {colors.Count} color value(s)");
                }
            }

            // If no smart content detected, classify as plain text or pure image
            if (result.DetectedTypes.Count == 0)
            {
                result.DetectedTypes.Add(hasText ? ContentType.PlainText : ContentType.PureImage);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Content classification failed: {ex.Message}");
            result.DetectedTypes.Add(hasText ? ContentType.PlainText : ContentType.PureImage);
        }

        return result;
    }
}
