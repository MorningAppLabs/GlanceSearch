# Privacy Policy

**Last Updated:** February 2026

GlanceSearch ("we", "our", or "the app") is designed with a strong commitment to your privacy. This policy explains how your data is handled when you use the GlanceSearch application for Windows.

## 1. Local Processing First
GlanceSearch runs directly on your local machine. The core functionality—including screen capture, image cropping, and Optical Character Recognition (OCR) using the Windows OCR engine—happens entirely offline on your device.

## 2. When Data Leaves Your Device
Data ONLY leaves your device when you explicitly request a web-based action. 

These actions include:
- **Searching the Web:** If you click "Search," the extracted text is sent to your preferred search engine (e.g., Google, Bing, DuckDuckGo) via your default web browser.
- **Translation:** If you click "Translate," the extracted text is sent to the translation provider you configured in settings (e.g., MyMemory API, DeepL, Google Cloud). 
- **Checking for Updates:** If enabled in Settings, the app periodically checks our GitHub repository for new releases.

We **never** silently upload your screen captures, extracted text, or usage data in the background.

## 3. Data Storage
- **Settings:** Your configuration and API keys are stored locally in `%LOCALAPPDATA%\GlanceSearch`. Sensitive API keys are encrypted using Windows DPAPI (Data Protection API) and cannot be read by other users.
- **History:** If you enable the "History" feature, your past captures are stored locally in an encrypted SQLite database on your device. You can clear this history at any time from the Settings window.

## 4. Third-Party Services
For network actions you initiate, you are subject to the privacy policies of the respective providers:
- Search Engines (Google, Bing, DuckDuckGo)
- Translation APIs (MyMemory, DeepL, etc.)

## 5. Contact
If you have any questions or concerns about this privacy policy, please open an issue on our GitHub repository.
