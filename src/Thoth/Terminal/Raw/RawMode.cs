using System.Runtime.InteropServices;

namespace Thoth.Terminal.Raw;

public static partial class RawMode
{
    const int STDIN_FILENO = 0;
    const int STDOUT_FILENO = 1;
    const int STDERR_FILENO = 2;
    const int TCSAFLUSH = 2;
    const int O_RDWR = 2;

    static bool _enabled;
    static bool _ownsFd;

    static termios_macos _origMac;
    static termios_linux _origLinux;
    public static int TtyFd { get; private set; } = -1;

    [LibraryImport("libc", EntryPoint = "tcgetattr", SetLastError = true)]
    private static partial int tcgetattr(int fd, out termios_macos t);

    [LibraryImport("libc", EntryPoint = "tcsetattr", SetLastError = true)]
    private static partial int tcsetattr(int fd, int actions, in termios_macos t);

    [LibraryImport("libc", EntryPoint = "tcgetattr", SetLastError = true)]
    private static partial int tcgetattr(int fd, out termios_linux t);

    [LibraryImport("libc", EntryPoint = "tcsetattr", SetLastError = true)]
    private static partial int tcsetattr(int fd, int actions, in termios_linux t);

    [LibraryImport("libc", EntryPoint = "cfmakeraw")]
    private static partial void cfmakeraw(ref termios_macos t);

    [LibraryImport("libc", EntryPoint = "cfmakeraw")]
    private static partial void cfmakeraw(ref termios_linux t);

    [LibraryImport("libc", EntryPoint = "isatty")]
    private static partial int isatty(int fd);

    [LibraryImport("libc",
                   EntryPoint = "open",
                   SetLastError = true,
                   StringMarshalling = StringMarshalling.Utf8)]
    private static partial int open(string path, int flags);

    [LibraryImport("libc", EntryPoint = "close")]
    private static partial int close(int fd);

    static int GetTtyFd()
    {
        if (isatty(STDIN_FILENO) != 0) return STDIN_FILENO;
        if (isatty(STDOUT_FILENO) != 0) return STDOUT_FILENO;
        if (isatty(STDERR_FILENO) != 0) return STDERR_FILENO;

        var fd = open("/dev/tty", O_RDWR);
        if (fd < 0)
            throw new InvalidOperationException(
                $"No controlling TTY found (errno {Marshal.GetLastPInvokeError()}).");
        _ownsFd = true;
        return fd;
    }

    public static void Enable(bool keepSignals = false)
    {
        if (_enabled) return;
        if (!(OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()))
            throw new PlatformNotSupportedException(
                "Raw mode is only supported on macOS and Linux.");

        TtyFd = GetTtyFd();

        if (OperatingSystem.IsMacOS())
        {
            if (tcgetattr(TtyFd, out _origMac) != 0)
                throw new InvalidOperationException(
                    $"Raw mode tcgetattr failed (errno {Marshal.GetLastPInvokeError()}).");

            var raw = _origMac;
            cfmakeraw(ref raw);

            if (keepSignals) raw.c_lflag |= TermiosBitsMacOS.ISIG;

            if (tcsetattr(TtyFd, TCSAFLUSH, in raw) != 0)
                throw new InvalidOperationException(
                    $"Raw mode tcsetattr failed (errno {Marshal.GetLastPInvokeError()}).");

            if (tcgetattr(TtyFd, out termios_macos verify) != 0)
                throw new InvalidOperationException(
                    $"Raw mode verify tcgetattr failed (errno {Marshal.GetLastPInvokeError()}).");

            if ((verify.c_lflag & TermiosBitsMacOS.ECHO) != 0)
                throw new InvalidOperationException(
                    $"Raw mode failed: ECHO bit still set after tcsetattr (c_lflag=0x{verify.c_lflag:X}).");

            _enabled = true;

            return;
        }

        // Linux
        if (tcgetattr(TtyFd, out _origLinux) != 0)
            throw new InvalidOperationException(
                $"Raw mode tcgetattr failed (errno {Marshal.GetLastPInvokeError()}).");

        var rawLinux = _origLinux;
        cfmakeraw(ref rawLinux);

        if (keepSignals) rawLinux.c_lflag |= TermiosBitsLinux.ISIG;

        if (tcsetattr(TtyFd, TCSAFLUSH, in rawLinux) != 0)
            throw new InvalidOperationException(
                $"Raw mode tcsetattr failed (errno {Marshal.GetLastPInvokeError()}).");

        _enabled = true;
    }

    public static void Disable()
    {
        if (!_enabled) return;

        if (OperatingSystem.IsMacOS())
            tcsetattr(TtyFd, TCSAFLUSH, in _origMac);
        else if (OperatingSystem.IsLinux()) tcsetattr(TtyFd, TCSAFLUSH, in _origLinux);

        if (_ownsFd && TtyFd >= 0)
        {
            close(TtyFd);
            TtyFd = -1;
            _ownsFd = false;
        }

        _enabled = false;
    }

    // macOS termios: tcflag_t and speed_t are unsigned long (8 bytes on 64-bit), c_cc[20], no c_line.
    // Total size: 72 bytes. Offsets: c_iflag=0, c_oflag=8, c_cflag=16, c_lflag=24, c_cc=32, c_ispeed=56, c_ospeed=64.
    // There is 4 bytes padding after c_cc[20] to align c_ispeed to 8-byte boundary.
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct termios_macos
    {
        public ulong c_iflag;
        public ulong c_oflag;
        public ulong c_cflag;
        public ulong c_lflag;

        public fixed byte c_cc[20];
        uint _padding;

        public ulong c_ispeed;
        public ulong c_ospeed;
    }

    // Linux termios: includes c_line, and c_cc[32].
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct termios_linux
    {
        public uint c_iflag;
        public uint c_oflag;
        public uint c_cflag;
        public uint c_lflag;

        public byte c_line;
        public fixed byte c_cc[32];

        public uint c_ispeed;
        public uint c_ospeed;
    }

    static class TermiosBitsMacOS
    {
        public const ulong ISIG = 0x00000080;
        public const ulong ECHO = 0x00000008;
    }

    static class TermiosBitsLinux
    {
        public const uint
            ISIG = 0x00000001; // NOTE: this value varies by libc/arch; verify if you actually need keepSignals on Linux
    }
}