#include <windows.h>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>

namespace fs = std::filesystem;

struct Entry { std::wstring path; unsigned long long offset; unsigned long long length; };
constexpr char magic[] = "USBLLM1";

void fail(const std::wstring& message) { MessageBoxW(nullptr, message.c_str(), L"Usb LLM", MB_OK | MB_ICONERROR); }

bool read_package(const fs::path& package, std::wstring& build_id, std::vector<Entry>& entries) {
    std::ifstream input(package, std::ios::binary | std::ios::ate);
    if (!input) return false;
    const auto size = static_cast<unsigned long long>(input.tellg());
    if (size < 16) return false;
    input.seekg(static_cast<std::streamoff>(size - 16));
    char trailer_magic[8]{}; unsigned long long index_size{};
    input.read(trailer_magic, 8); input.read(reinterpret_cast<char*>(&index_size), 8);
    if (std::string(trailer_magic, 7) != magic || index_size > size - 16) return false;
    input.seekg(static_cast<std::streamoff>(size - 16 - index_size));
    std::string index(index_size, '\0'); input.read(index.data(), static_cast<std::streamsize>(index_size));
    std::istringstream lines(index); std::string line;
    if (!std::getline(lines, line) || line.rfind("build=", 0) != 0) return false;
    build_id.assign(line.begin() + 6, line.end());
    while (std::getline(lines, line)) {
        const auto first = line.find('|'), second = line.find('|', first + 1);
        if (first == std::string::npos || second == std::string::npos) return false;
        Entry entry;
        entry.path.assign(line.begin(), line.begin() + static_cast<long long>(first));
        entry.offset = std::stoull(line.substr(first + 1, second - first - 1));
        entry.length = std::stoull(line.substr(second + 1));
        entries.push_back(entry);
    }
    return !entries.empty();
}

bool extract(const fs::path& package, const fs::path& root, const Entry& entry) {
    const fs::path target = root / entry.path;
    if (fs::exists(target) && fs::file_size(target) == entry.length) return true;
    fs::create_directories(target.parent_path());
    const fs::path partial = target.wstring() + L".part";
    std::ifstream source(package, std::ios::binary);
    std::ofstream destination(partial, std::ios::binary | std::ios::trunc);
    if (!source || !destination) return false;
    source.seekg(static_cast<std::streamoff>(entry.offset));
    std::vector<char> buffer(4 * 1024 * 1024);
    unsigned long long remaining = entry.length;
    while (remaining) {
        const auto chunk = static_cast<std::streamsize>(std::min<unsigned long long>(remaining, buffer.size()));
        source.read(buffer.data(), chunk);
        if (source.gcount() != chunk) return false;
        destination.write(buffer.data(), chunk);
        if (!destination) return false;
        remaining -= static_cast<unsigned long long>(chunk);
    }
    destination.close();
    std::error_code error; fs::rename(partial, target, error);
    if (error) { fs::remove(target, error); fs::rename(partial, target, error); }
    return !error;
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
    wchar_t executable[MAX_PATH]{};
    GetModuleFileNameW(nullptr, executable, MAX_PATH);
    const fs::path package(executable);
    std::wstring build_id; std::vector<Entry> entries;
    if (!read_package(package, build_id, entries)) { fail(L"This Usb LLM package is incomplete or damaged."); return 1; }
    const fs::path root = package.parent_path() / L".usbllm-cache" / build_id;
    const bool first_run = !fs::exists(root);
    if (first_run) {
        MessageBoxW(nullptr,
            L"Usb LLM is preparing its private local runtime for the first time. To run quickly and remain fully offline, it is extracting the included models and acceleration files to a hidden folder next to this EXE. This may take a few minutes. You may delete that hidden folder whenever you want; keep UsbLLM.exe and it will recreate it offline the next time you run it.",
            L"Preparing Usb LLM", MB_OK | MB_ICONINFORMATION);
        fs::create_directories(root);
        SetFileAttributesW((package.parent_path() / L".usbllm-cache").c_str(), FILE_ATTRIBUTE_HIDDEN);
    }
    for (const auto& entry : entries) if (!extract(package, root, entry)) { fail(L"Usb LLM could not extract a required local file. Check available disk space and try again."); return 1; }
    const std::wstring command = L"\"" + (root / L"UsbLlm.exe").wstring() + L"\"";
    STARTUPINFOW startup{ sizeof(startup) }; PROCESS_INFORMATION process{};
    if (!CreateProcessW(nullptr, const_cast<wchar_t*>(command.c_str()), nullptr, nullptr, FALSE, 0, nullptr, root.c_str(), &startup, &process)) { fail(L"Usb LLM could not start its extracted local application."); return 1; }
    CloseHandle(process.hThread); CloseHandle(process.hProcess);
    return 0;
}
