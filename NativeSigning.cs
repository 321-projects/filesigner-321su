using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace FileSigner321
{
    internal static class NativeSigning
    {
        private const uint NoWizardUi = 1;
        private const uint SubjectIsFile = 1;
        private const uint UseCertificateContext = 1;
        private const uint IncludeChainWithoutRoot = 2;
        private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

        internal static Func<string> SignAuthenticode(string path, X509Certificate2 certificate)
        {
            var extended = new SignExtendedInfo
            {
                Size = (uint)Marshal.SizeOf(typeof(SignExtendedInfo)),
                HashAlgorithm = Marshal.StringToHGlobalAnsi(Sha256Oid)
            };
            IntPtr memory = IntPtr.Zero;
            IntPtr context = IntPtr.Zero;
            try
            {
                memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SignExtendedInfo)));
                Marshal.StructureToPtr(extended, memory, false);
                var info = new SignInfo
                {
                    Size = (uint)Marshal.SizeOf(typeof(SignInfo)),
                    SubjectChoice = SubjectIsFile,
                    FileName = path,
                    CertificateChoice = UseCertificateContext,
                    CertificateContext = certificate.Handle,
                    AdditionalCertChoice = IncludeChainWithoutRoot,
                    ExtendedInfo = memory
                };
                bool signed = CryptUIWizDigitalSign(NoWizardUi, IntPtr.Zero, null, ref info, out context);
                int error = Marshal.GetLastWin32Error();
                GC.KeepAlive(certificate);
                if (!signed) throw new Win32Exception(error, "Authenticode: " + new Win32Exception(error).Message);
            }
            finally
            {
                if (context != IntPtr.Zero) CryptUIWizFreeDigitalSignContext(context);
                if (memory != IntPtr.Zero) Marshal.FreeHGlobal(memory);
                Marshal.FreeHGlobal(extended.HashAlgorithm);
            }

            uint trustStatus = Verify(path);
            if (trustStatus == 0) return () => UiText.Get("Подпись проверена");
            if (trustStatus == 0x800B0109 || trustStatus == 0x800B010A || trustStatus == 0x800B0004)
                return () => string.Format(UiText.Get("Подписан, доверие не подтверждено (0x{0:X8})"), trustStatus);
            throw new InvalidOperationException(string.Format("Windows не подтвердила подпись (0x{0:X8}).", trustStatus));
        }

        private static uint Verify(string path)
        {
            IntPtr name = Marshal.StringToHGlobalUni(path);
            IntPtr memory = IntPtr.Zero;
            try
            {
                var file = new TrustFile
                {
                    Size = (uint)Marshal.SizeOf(typeof(TrustFile)),
                    FilePath = name
                };
                memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TrustFile)));
                Marshal.StructureToPtr(file, memory, false);
                var data = new TrustData
                {
                    Size = (uint)Marshal.SizeOf(typeof(TrustData)),
                    UiChoice = 2,              // WTD_UI_NONE
                    UnionChoice = 1,           // WTD_CHOICE_FILE
                    File = memory,
                    StateAction = 1,           // WTD_STATEACTION_VERIFY
                    ProviderFlags = 0x1010     // Local cached trust only, no revocation lookup
                };
                var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
                try { return unchecked((uint)WinVerifyTrust(new IntPtr(-1), ref action, ref data)); }
                finally
                {
                    if (data.StateData != IntPtr.Zero)
                    {
                        data.StateAction = 2;  // WTD_STATEACTION_CLOSE
                        WinVerifyTrust(new IntPtr(-1), ref action, ref data);
                    }
                }
            }
            finally
            {
                if (memory != IntPtr.Zero) Marshal.FreeHGlobal(memory);
                Marshal.FreeHGlobal(name);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SignInfo
        {
            internal uint Size;
            internal uint SubjectChoice;
            [MarshalAs(UnmanagedType.LPWStr)] internal string FileName;
            internal uint CertificateChoice;
            internal IntPtr CertificateContext;
            [MarshalAs(UnmanagedType.LPWStr)] internal string TimestampUrl;
            internal uint AdditionalCertChoice;
            internal IntPtr ExtendedInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SignExtendedInfo
        {
            internal uint Size;
            internal uint AttributeFlags;
            internal IntPtr Description;
            internal IntPtr MoreInfoLocation;
            internal IntPtr HashAlgorithm;
            internal IntPtr CertificateDisplayString;
            internal IntPtr AdditionalCertStore;
            internal IntPtr AuthenticatedAttributes;
            internal IntPtr UnauthenticatedAttributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TrustFile
        {
            internal uint Size;
            internal IntPtr FilePath;
            internal IntPtr FileHandle;
            internal IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TrustData
        {
            internal uint Size;
            internal IntPtr PolicyCallbackData;
            internal IntPtr SipClientData;
            internal uint UiChoice;
            internal uint RevocationChecks;
            internal uint UnionChoice;
            internal IntPtr File;
            internal uint StateAction;
            internal IntPtr StateData;
            internal IntPtr UrlReference;
            internal uint ProviderFlags;
            internal uint UiContext;
            internal IntPtr SignatureSettings;
        }

        [DllImport("cryptui.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUIWizDigitalSign(uint flags, IntPtr parent, string title,
            ref SignInfo info, out IntPtr context);

        [DllImport("cryptui.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUIWizFreeDigitalSignContext(IntPtr context);

        [DllImport("wintrust.dll", ExactSpelling = true)]
        private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref TrustData data);
    }
}
