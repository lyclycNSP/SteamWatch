#define UNICODE
#define _UNICODE
#include <windows.h>
#include <strsafe.h>

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previous, PWSTR arguments, int show)
{
    (void)instance; (void)previous; (void)show;
    WCHAR directory[32768];
    WCHAR executable[32768];
    WCHAR command[32768];
    DWORD length = GetModuleFileNameW(NULL, directory, ARRAYSIZE(directory));
    if (length == 0 || length >= ARRAYSIZE(directory)) goto failed;
    WCHAR *separator = wcsrchr(directory, L'\\');
    if (separator == NULL) goto failed;
    *separator = L'\0';
    if (FAILED(StringCchPrintfW(executable, ARRAYSIZE(executable), L"%s\\app\\SteamWatch.exe", directory))) goto failed;
    // Explicit executable path and quoting prevent spaces from changing the program being launched.
    if (FAILED(StringCchPrintfW(command, ARRAYSIZE(command), L"\"%s\" %s", executable, arguments))) goto failed;
    STARTUPINFOW startup = {0};
    PROCESS_INFORMATION process = {0};
    startup.cb = sizeof(startup);
    if (!CreateProcessW(executable, command, NULL, NULL, FALSE, 0, NULL, directory, &startup, &process)) goto failed;
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    return 0;

failed:
    MessageBoxW(NULL,
        L"\x65e0\x6cd5\x542f\x52a8 SteamWatch\x3002\n\n"
        L"\x8bf7\x5148\x5b8c\x6574\x89e3\x538b ZIP\xff0c\x4fdd\x7559\x542f\x52a8\x7a0b\x5e8f\x4e0e app \x6587\x4ef6\x5939\x7684\x76f8\x5bf9\x4f4d\x7f6e\xff0c\x7136\x540e\x91cd\x8bd5\x3002",
        L"SteamWatch", MB_OK | MB_ICONERROR);
    return 1;
}
