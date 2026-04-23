using System;
using System.Runtime.InteropServices;
using System.Text;

public static class WindowsDpapi
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CRYPTPROTECT_PROMPTSTRUCT
    {
        public int cbSize;
        public int dwPromptFlags;
        public IntPtr hwndApp;
        public string szPrompt;
    }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptProtectData(
        ref DATA_BLOB pDataIn,
        string szDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        ref CRYPTPROTECT_PROMPTSTRUCT pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CryptUnprotectData(
        ref DATA_BLOB pDataIn,
        IntPtr ppszDataDescr,
        ref DATA_BLOB pOptionalEntropy,
        IntPtr pvReserved,
        ref CRYPTPROTECT_PROMPTSTRUCT pPromptStruct,
        int dwFlags,
        ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static byte[] Protect(byte[] plainBytes, byte[] entropyBytes)
    {
        return Transform(plainBytes, entropyBytes, true);
    }

    public static byte[] Unprotect(byte[] cipherBytes, byte[] entropyBytes)
    {
        return Transform(cipherBytes, entropyBytes, false);
    }

    private static byte[] Transform(byte[] inputBytes, byte[] entropyBytes, bool protect)
    {
        DATA_BLOB inputBlob = CreateBlob(inputBytes);
        DATA_BLOB entropyBlob = CreateBlob(entropyBytes);
        DATA_BLOB outputBlob = default;
        CRYPTPROTECT_PROMPTSTRUCT prompt = new CRYPTPROTECT_PROMPTSTRUCT
        {
            cbSize = Marshal.SizeOf<CRYPTPROTECT_PROMPTSTRUCT>(),
            dwPromptFlags = 0,
            hwndApp = IntPtr.Zero,
            szPrompt = null
        };

        try
        {
            bool success = protect
                ? CryptProtectData(ref inputBlob, null, ref entropyBlob, IntPtr.Zero, ref prompt, 0, ref outputBlob)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, ref prompt, 0, ref outputBlob);

            if (!success)
                throw new InvalidOperationException("DPAPI call failed: " + Marshal.GetLastWin32Error());

            byte[] output = new byte[outputBlob.cbData];
            Marshal.Copy(outputBlob.pbData, output, 0, outputBlob.cbData);
            return output;
        }
        finally
        {
            FreeAllocatedBlob(ref inputBlob);
            FreeAllocatedBlob(ref entropyBlob);
            FreeCryptBlob(ref outputBlob);
        }
    }

    private static DATA_BLOB CreateBlob(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            bytes = Encoding.UTF8.GetBytes(string.Empty);

        DATA_BLOB blob = new DATA_BLOB
        {
            cbData = bytes.Length,
            pbData = Marshal.AllocHGlobal(bytes.Length)
        };
        Marshal.Copy(bytes, 0, blob.pbData, bytes.Length);
        return blob;
    }

    private static void FreeAllocatedBlob(ref DATA_BLOB blob)
    {
        if (blob.pbData == IntPtr.Zero)
            return;

        Marshal.FreeHGlobal(blob.pbData);
        blob.pbData = IntPtr.Zero;
        blob.cbData = 0;
    }

    private static void FreeCryptBlob(ref DATA_BLOB blob)
    {
        if (blob.pbData == IntPtr.Zero)
            return;

        LocalFree(blob.pbData);
        blob.pbData = IntPtr.Zero;
        blob.cbData = 0;
    }
}
