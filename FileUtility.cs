using System.IO;
using System.Runtime.InteropServices;

public static class FileUtility
{
    // Define the Windows API structure for SHFileOperation
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpszProgressTitle;
    }

    // Define the function code for deletion
    private const uint FO_DELETE = 0x0003;

    // Define the operation flags
    private const ushort FOF_ALLOWUNDO = 0x0040; // Send to Recycle Bin
    private const ushort FOF_NOCONFIRMATION = 0x0010; // Suppress confirmation dialog
    private const ushort FOF_SILENT = 0x0004; // Do not display a progress box
    private const ushort FOF_NOERRORUI = 0x0400; // Do not display an error message box

    // Import the native Windows function
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    // Moves a file to the Windows Recycle Bin using the native Shell API.
    // <param name="path">The full path of the file to delete.</param>
    // <returns>True if the operation was successful, false otherwise.</returns>
    public static bool MoveToRecycleBin(string path)
    {
        string safePath = path; // 1. Strip the URI if it exists
        if (safePath.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            safePath = safePath[8..].Replace('/', '\\');
        }

        // 2. Apply the Long Path prefix for Windows API safety
        if (!safePath.StartsWith(@"\\?\"))
        {
            safePath = @"\\?\" + safePath;
        }

        if (!File.Exists(path)) return false;

        // The pFrom string must be double-null terminated for the API.
        string pFrom = path + '\0' + '\0';

        SHFILEOPSTRUCT fileOp = new SHFILEOPSTRUCT
        {
            hwnd = IntPtr.Zero,
            wFunc = FO_DELETE,
            pFrom = pFrom,
            fFlags = FOF_ALLOWUNDO | FOF_NOERRORUI | FOF_SILENT
        };

        // SHFileOperation returns 0 on success.
        int result = SHFileOperation(ref fileOp);
        return result == 0;
    }
}
