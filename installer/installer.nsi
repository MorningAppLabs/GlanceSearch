; ============================================================
;  GlanceSearch NSIS Installer
;  Free for commercial use — NSIS is open source (zlib license)
;
;  Prerequisites: NSIS 3.x with Modern UI 2
;  Build:
;    1. dotnet publish src\GlanceSearch.App -c Release -r win-x64 ^
;          --self-contained true -p:PublishSingleFile=true
;    2. & "C:\Program Files (x86)\NSIS\makensis.exe" installer\installer.nsi
; ============================================================

; ─── Metadata ────────────────────────────────────────────────────────────────
!define APP_NAME      "GlanceSearch"
!define APP_VERSION   "1.0.0"
!define APP_PUBLISHER "Morning App Labs"
!define APP_URL       "https://github.com/MorningAppLabs/GlanceSearch"
!define APP_EXE       "GlanceSearch.exe"
!define REGKEY        "Software\${APP_NAME}"
!define UNINST_KEY    "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

; ─── Source ──────────────────────────────────────────────────────────────────
!define SOURCE_DIR    "..\src\GlanceSearch.App\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
!define ICON_FILE     "..\src\GlanceSearch.App\Assets\app.ico"

; ─── Output ──────────────────────────────────────────────────────────────────
!define OUTPUT_FILE   "..\build\GlanceSearchSetup-${APP_VERSION}.exe"

; ─── Global settings ─────────────────────────────────────────────────────────
Unicode True
SetCompressor /SOLID lzma
RequestExecutionLevel admin

!include "MUI2.nsh"
!include "Sections.nsh"

Name          "${APP_NAME} ${APP_VERSION}"
OutFile       "${OUTPUT_FILE}"
InstallDir    "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "${REGKEY}" "InstallDir"
BrandingText  "${APP_PUBLISHER}"

; ─── MUI appearance ──────────────────────────────────────────────────────────
!define MUI_ABORTWARNING
!define MUI_ICON   "${ICON_FILE}"
!define MUI_UNICON "${ICON_FILE}"

!define MUI_WELCOMEPAGE_TITLE    "Welcome to ${APP_NAME} ${APP_VERSION} Setup"
!define MUI_WELCOMEPAGE_TEXT     "This wizard will install ${APP_NAME} on your computer.$\r$\n$\r$\nGlanceSearch is a system-wide floating search utility. Capture any area of your screen, extract text, and take action—instantly.$\r$\n$\r$\nClick Next to continue."

!define MUI_FINISHPAGE_RUN          "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT     "Launch ${APP_NAME}"
!define MUI_FINISHPAGE_LINK         "Visit the project page"
!define MUI_FINISHPAGE_LINK_LOCATION "${APP_URL}"

; ─── Installer pages ─────────────────────────────────────────────────────────
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

; ─── Uninstaller pages ───────────────────────────────────────────────────────
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

; ─── Sections ─────────────────────────────────────────────────────────────────

Section "GlanceSearch (required)" SecMain
    SectionIn RO  ; Read-only — always installed

    SetOutPath "$INSTDIR"
    ; Copy all published files, skip .pdb debug symbols
    File /r /x "*.pdb" "${SOURCE_DIR}\*.*"

    ; Registry: install location + version
    WriteRegStr HKLM "${REGKEY}" "InstallDir" "$INSTDIR"
    WriteRegStr HKLM "${REGKEY}" "Version"    "${APP_VERSION}"

    ; Add/Remove Programs entry
    WriteRegStr   HKLM "${UNINST_KEY}" "DisplayName"     "${APP_NAME}"
    WriteRegStr   HKLM "${UNINST_KEY}" "DisplayVersion"  "${APP_VERSION}"
    WriteRegStr   HKLM "${UNINST_KEY}" "Publisher"       "${APP_PUBLISHER}"
    WriteRegStr   HKLM "${UNINST_KEY}" "URLInfoAbout"    "${APP_URL}"
    WriteRegStr   HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegStr   HKLM "${UNINST_KEY}" "DisplayIcon"     "$INSTDIR\${APP_EXE}"
    WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify"        1
    WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair"        1

    ; Start Menu shortcuts
    CreateDirectory "$SMPROGRAMS\${APP_NAME}"
    CreateShortcut  "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" \
                    "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}"
    CreateShortcut  "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" \
                    "$INSTDIR\Uninstall.exe"

    ; Write the uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Desktop Shortcut" SecDesktop
    CreateShortcut "$DESKTOP\${APP_NAME}.lnk" \
                   "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}"
SectionEnd

Section "Start with Windows" SecStartup
    WriteRegStr HKCU \
        "Software\Microsoft\Windows\CurrentVersion\Run" \
        "${APP_NAME}" "$INSTDIR\${APP_EXE}"
SectionEnd

; ─── Section descriptions ────────────────────────────────────────────────────
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SecMain}    "The core GlanceSearch application files. Required."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Add a shortcut to your Desktop."
    !insertmacro MUI_DESCRIPTION_TEXT ${SecStartup} "Start GlanceSearch automatically when Windows logs in."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; ─── Uninstaller ─────────────────────────────────────────────────────────────
Section "Uninstall"
    ; Remove installed files
    RMDir /r "$INSTDIR"

    ; Remove shortcuts
    Delete    "$DESKTOP\${APP_NAME}.lnk"
    RMDir /r  "$SMPROGRAMS\${APP_NAME}"

    ; Remove startup entry (written to HKCU — no elevation needed)
    DeleteRegValue HKCU \
        "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_NAME}"

    ; Remove registry keys
    DeleteRegKey HKLM "${UNINST_KEY}"
    DeleteRegKey HKLM "${REGKEY}"
SectionEnd
