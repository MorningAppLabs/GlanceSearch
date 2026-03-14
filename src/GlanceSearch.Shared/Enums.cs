namespace GlanceSearch.Shared;

public enum SelectionMode
{
    Freehand,
    Rectangle
}

public enum OcrEngineType
{
    WindowsBuiltIn,
    Tesseract,
    Cloud
}

public enum SearchEngine
{
    Google,
    Bing,
    DuckDuckGo,
    Brave,
    Custom
}

public enum AppTheme
{
    Light,
    Dark,
    System
}

public enum NotificationStyle
{
    Toast,
    Subtle,
    Off
}

public enum ContentType
{
    PlainText,
    Url,
    Email,
    PhoneNumber,
    CodeSnippet,
    QrCode,
    Barcode,
    MathExpression,
    ColorValue,
    Address,
    PureImage
}

public enum CaptureState
{
    Idle,
    Capturing,
    Selecting,
    Processing,
    ShowingResults
}
