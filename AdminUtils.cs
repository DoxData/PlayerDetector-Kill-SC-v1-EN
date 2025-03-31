using System.Security.Principal;
using System.Diagnostics;

namespace PlayerDetector-Kill-SC-v1-EN
{
    public static class AdminUtils
    {
        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void RestartAsAdministrator()
        {
            var exeName = Process.GetCurrentProcess().MainModule.FileName;
            var startInfo = new ProcessStartInfo(exeName)
            {
                Verb = "runas",
                UseShellExecute = true
            };
            try
            {
                Process.Start(startInfo);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                // Handle exception as needed
            }
        }
    }
}
// Copyright (c) 2024 DoxData. Exclusive ownership. 
// Prohibited use/modification without express authorization.
