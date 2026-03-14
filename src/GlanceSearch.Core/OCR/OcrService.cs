using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using GlanceSearch.Shared.Models;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinOcr = Windows.Media.Ocr;

namespace GlanceSearch.Core.OCR;

/// <summary>
/// OCR service supporting Windows.Media.Ocr (WinRT) and Tesseract engines.
/// Provides enhanced pre/post processing for high-accuracy text extraction.
/// </summary>
public class OcrService
{
    private WinOcr.OcrEngine? _ocrEngine;
    private Tesseract.TesseractEngine? _tesseractEngine;
    private string _tesseractDataPath = string.Empty;
    private string _currentEngine = "windows";
    private string _currentLanguage = "en-US";

    /// <summary>
    /// Initialize the OCR engine(s) for the specified language.
    /// </summary>
    public void Initialize(string language = "en-US")
    {
        _currentLanguage = language;
        InitializeWindowsOcr(language);
        InitializeTesseract();
    }

    private void InitializeWindowsOcr(string language)
    {
        try
        {
            var lang = new Windows.Globalization.Language(language);
            if (WinOcr.OcrEngine.IsLanguageSupported(lang))
            {
                _ocrEngine = WinOcr.OcrEngine.TryCreateFromLanguage(lang);
            }
            else
            {
                _ocrEngine = WinOcr.OcrEngine.TryCreateFromUserProfileLanguages();
            }
        }
        catch
        {
            _ocrEngine = WinOcr.OcrEngine.TryCreateFromUserProfileLanguages();
        }
    }

    private void InitializeTesseract()
    {
        try
        {
            // Look for tessdata in multiple standard locations
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlanceSearch", "tessdata"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", "tessdata"),
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path) && Directory.GetFiles(path, "*.traineddata").Length > 0)
                {
                    _tesseractDataPath = path;
                    break;
                }
            }

            // If no tessdata found, create the directory
            if (string.IsNullOrEmpty(_tesseractDataPath))
            {
                _tesseractDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                Directory.CreateDirectory(_tesseractDataPath);
            }

            // Only create engine if traineddata exists
            var engData = Path.Combine(_tesseractDataPath, "eng.traineddata");
            if (File.Exists(engData))
            {
                _tesseractEngine = new Tesseract.TesseractEngine(_tesseractDataPath, "eng", Tesseract.EngineMode.Default);
                _tesseractEngine.SetVariable("tessedit_pageseg_mode", "6"); // Assume uniform block of text
                _tesseractEngine.SetVariable("preserve_interword_spaces", "1");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tesseract initialization failed: {ex.Message}");
            _tesseractEngine = null;
        }
    }

    /// <summary>
    /// Set the active OCR engine.
    /// </summary>
    public void SetEngine(string engine)
    {
        _currentEngine = engine?.ToLowerInvariant() ?? "windows";
    }

    /// <summary>
    /// Check if Tesseract is available (traineddata exists).
    /// </summary>
    public bool IsTesseractAvailable => _tesseractEngine != null;

    /// <summary>
    /// Get the tessdata path for downloading traineddata files.
    /// </summary>
    public string TessdataPath => _tesseractDataPath;

    /// <summary>
    /// Download Tesseract trained data if not present.
    /// </summary>
    public async Task EnsureTesseractDataAsync()
    {
        var engData = Path.Combine(_tesseractDataPath, "eng.traineddata");
        if (File.Exists(engData)) return;

        Directory.CreateDirectory(_tesseractDataPath);

        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);
            var url = "https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata";
            var data = await httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(engData, data);

            // Re-initialize Tesseract engine after download
            InitializeTesseract();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to download Tesseract data: {ex.Message}");
        }
    }

    /// <summary>
    /// Extract text from a Bitmap image using the configured OCR engine.
    /// Falls back to Windows OCR if Tesseract is unavailable.
    /// </summary>
    public async Task<OcrResult> RecognizeAsync(Bitmap bitmap)
    {
        // Enhanced pre-processing for both engines
        var processedBitmap = PreProcess(bitmap);

        try
        {
            if (_currentEngine == "tesseract" && _tesseractEngine != null)
            {
                return RecognizeWithTesseract(processedBitmap);
            }
            else
            {
                return await RecognizeWithWindowsOcr(processedBitmap);
            }
        }
        finally
        {
            // Dispose the processed bitmap if it's a new one (not the original)
            if (processedBitmap != bitmap)
            {
                processedBitmap.Dispose();
            }
        }
    }

    /// <summary>
    /// Recognize text using Windows.Media.Ocr (WinRT).
    /// </summary>
    private async Task<OcrResult> RecognizeWithWindowsOcr(Bitmap bitmap)
    {
        if (_ocrEngine == null)
            Initialize();

        if (_ocrEngine == null)
        {
            return new OcrResult
            {
                ExtractedText = "",
                Confidence = 0,
                EngineUsed = "Windows (Failed to initialize)"
            };
        }

        var softwareBitmap = await ConvertToSoftwareBitmapAsync(bitmap);
        var ocrResult = await _ocrEngine.RecognizeAsync(softwareBitmap);
        return PostProcessWindowsOcr(ocrResult);
    }

    /// <summary>
    /// Recognize text using Tesseract OCR.
    /// </summary>
    private OcrResult RecognizeWithTesseract(Bitmap bitmap)
    {
        if (_tesseractEngine == null)
        {
            return new OcrResult
            {
                ExtractedText = "",
                Confidence = 0,
                EngineUsed = "Tesseract (Not initialized — download traineddata from Settings)"
            };
        }

        try
        {
            // Convert Bitmap to Pix for Tesseract
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;

            using var pix = Tesseract.Pix.LoadFromMemory(ms.ToArray());
            using var page = _tesseractEngine.Process(pix);

            var text = page.GetText();
            var confidence = page.GetMeanConfidence();

            var result = new OcrResult
            {
                ExtractedText = CleanText(text),
                Confidence = confidence,
                EngineUsed = "Tesseract",
                DetectedLanguage = _currentLanguage
            };

            // Extract line-level info
            using var iter = page.GetIterator();
            var lines = new List<OcrLine>();
            iter.Begin();
            do
            {
                var lineText = iter.GetText(Tesseract.PageIteratorLevel.TextLine);
                if (!string.IsNullOrWhiteSpace(lineText))
                {
                    lines.Add(new OcrLine
                    {
                        Text = lineText.Trim(),
                        Confidence = confidence
                    });
                }
            } while (iter.Next(Tesseract.PageIteratorLevel.TextLine));

            result.Lines = lines;
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tesseract OCR failed: {ex.Message}");
            return new OcrResult
            {
                ExtractedText = "",
                Confidence = 0,
                EngineUsed = $"Tesseract (Error: {ex.Message})"
            };
        }
    }

    /// <summary>
    /// Enhanced pre-processing for better OCR accuracy on both engines.
    /// Applies adaptive upscaling, contrast enhancement, and grayscale conversion.
    /// </summary>
    private Bitmap PreProcess(Bitmap source)
    {
        // Step 1: Upscale small images for better character recognition
        var needsUpscale = source.Width < 2000 && source.Height < 2000;
        var scale = needsUpscale ? 2.0 : 1.0;
        // For very small images, upscale more aggressively
        if (source.Width < 500 || source.Height < 500) scale = 3.0;

        var newWidth = (int)(source.Width * scale);
        var newHeight = (int)(source.Height * scale);

        var processed = new Bitmap(newWidth, newHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        processed.SetResolution(300, 300); // Set to 300 DPI for better OCR

        using (var g = Graphics.FromImage(processed))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(source, 0, 0, newWidth, newHeight);
        }

        // Step 2: Convert to grayscale for better OCR (reduces noise from color)
        processed = ConvertToGrayscale(processed);

        // Step 3: Apply contrast enhancement
        processed = EnhanceContrast(processed);

        return processed;
    }

    /// <summary>
    /// Convert image to grayscale for cleaner OCR input.
    /// </summary>
    private Bitmap ConvertToGrayscale(Bitmap source)
    {
        var grayscale = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        grayscale.SetResolution(source.HorizontalResolution, source.VerticalResolution);

        using var g = Graphics.FromImage(grayscale);
        var colorMatrix = new ColorMatrix(new float[][]
        {
            new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
            new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
            new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { 0, 0, 0, 0, 1 }
        });

        using var attrs = new ImageAttributes();
        attrs.SetColorMatrix(colorMatrix);
        g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height),
            0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);

        source.Dispose();
        return grayscale;
    }

    /// <summary>
    /// Enhance contrast to make text stand out better from background.
    /// </summary>
    private Bitmap EnhanceContrast(Bitmap source)
    {
        float contrast = 1.3f; // Moderate contrast boost
        float t = (1.0f - contrast) / 2.0f;

        var contrastMatrix = new ColorMatrix(new float[][]
        {
            new float[] { contrast, 0, 0, 0, 0 },
            new float[] { 0, contrast, 0, 0, 0 },
            new float[] { 0, 0, contrast, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { t, t, t, 0, 1 }
        });

        var enhanced = new Bitmap(source.Width, source.Height, source.PixelFormat);
        enhanced.SetResolution(source.HorizontalResolution, source.VerticalResolution);

        using var g = Graphics.FromImage(enhanced);
        using var attrs = new ImageAttributes();
        attrs.SetColorMatrix(contrastMatrix);
        g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height),
            0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);

        source.Dispose();
        return enhanced;
    }

    /// <summary>
    /// Convert System.Drawing.Bitmap to WinRT SoftwareBitmap.
    /// </summary>
    private async Task<SoftwareBitmap> ConvertToSoftwareBitmapAsync(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Bmp);
        ms.Position = 0;

        var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
    }

    /// <summary>
    /// Post-process Windows OCR result.
    /// </summary>
    private OcrResult PostProcessWindowsOcr(WinOcr.OcrResult winOcrResult)
    {
        var result = new OcrResult
        {
            EngineUsed = "Windows"
        };

        if (winOcrResult.Lines.Count == 0)
        {
            result.ExtractedText = "";
            result.Confidence = 0;
            return result;
        }

        var lines = new List<OcrLine>();
        var textLines = new List<string>();

        foreach (var line in winOcrResult.Lines)
        {
            var lineText = line.Text.Trim();
            if (!string.IsNullOrEmpty(lineText))
            {
                textLines.Add(lineText);
                lines.Add(new OcrLine
                {
                    Text = lineText,
                    Confidence = 0.85
                });
            }
        }

        result.ExtractedText = CleanText(string.Join(Environment.NewLine, textLines));
        result.Lines = lines;
        result.Confidence = 0.85;
        result.DetectedLanguage = winOcrResult.TextAngle.HasValue ? "detected" : _currentLanguage;

        return result;
    }

    /// <summary>
    /// Clean up extracted text: fix common OCR artifacts, normalize whitespace.
    /// </summary>
    private string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lines = text.Split('\n');
        var cleaned = lines.Select(line =>
        {
            var result = System.Text.RegularExpressions.Regex.Replace(line, @"  +", " ");
            // Fix common OCR character substitutions
            result = result.Replace("\u2018", "'")
                           .Replace("\u2019", "'")
                           .Replace("\u201C", "\"")
                           .Replace("\u201D", "\"");
            return result.TrimEnd();
        });

        // Remove empty lines at start/end, but preserve internal paragraph breaks
        var cleanedLines = cleaned.ToList();
        while (cleanedLines.Count > 0 && string.IsNullOrWhiteSpace(cleanedLines[0]))
            cleanedLines.RemoveAt(0);
        while (cleanedLines.Count > 0 && string.IsNullOrWhiteSpace(cleanedLines[^1]))
            cleanedLines.RemoveAt(cleanedLines.Count - 1);

        return string.Join(Environment.NewLine, cleanedLines);
    }

    /// <summary>
    /// Get list of available Windows OCR languages on this system.
    /// </summary>
    public static IReadOnlyList<Windows.Globalization.Language> GetAvailableLanguages()
    {
        return WinOcr.OcrEngine.AvailableRecognizerLanguages;
    }
}
