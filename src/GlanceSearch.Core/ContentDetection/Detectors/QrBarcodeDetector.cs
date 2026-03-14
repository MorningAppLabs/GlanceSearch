using System.Diagnostics;
using ZXing;
using ZXing.Windows.Compatibility;

namespace GlanceSearch.Core.ContentDetection.Detectors;

/// <summary>
/// Detects QR codes and barcodes in captured images using ZXing.Net.
/// </summary>
public static class QrBarcodeDetector
{
    /// <summary>
    /// Attempts to decode QR codes or barcodes from a System.Drawing.Bitmap.
    /// Returns decoded content or null if nothing found.
    /// </summary>
    public static string? Detect(System.Drawing.Bitmap? bitmap)
    {
        if (bitmap is null) return null;

        try
        {
            var reader = new BarcodeReader
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    TryInverted = true,
                    PossibleFormats = new List<BarcodeFormat>
                    {
                        BarcodeFormat.QR_CODE,
                        BarcodeFormat.DATA_MATRIX,
                        BarcodeFormat.CODE_128,
                        BarcodeFormat.CODE_39,
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.UPC_A,
                        BarcodeFormat.UPC_E,
                    }
                }
            };

            var result = reader.Decode(bitmap);
            return result?.Text;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"QR/Barcode detection failed: {ex.Message}");
            return null;
        }
    }
}
