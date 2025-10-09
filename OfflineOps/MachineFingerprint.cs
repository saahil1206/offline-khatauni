using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OfflineOps
{
    public class MachineFingerprint
    {
        public static string GetMachineId()
        {
            StringBuilder fingerprint = new StringBuilder();

            // Get machine name (reliable)
            fingerprint.Append(GetMachineName());
            fingerprint.Append("|");

            // Get username (reliable)
            fingerprint.Append(GetUsername());
            fingerprint.Append("|");

            // Get processor info (usually available)
            fingerprint.Append(GetProcessorInfo());
            fingerprint.Append("|");

            // Get volume serial number (reliable, no admin needed)
            fingerprint.Append(GetVolumeSerial());
            fingerprint.Append("|");

            // Get Windows product ID (stable)
            fingerprint.Append(GetWindowsProductId());

            // Create hash of the fingerprint
            return CreateHash(fingerprint.ToString());
        }

        private static string GetMachineName()
        {
            try
            {
                return Environment.MachineName;
            }
            catch
            {
                return "";
            }
        }

        private static string GetProcessorInfo()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString() ?? "";
                    string cores = obj["NumberOfCores"]?.ToString() ?? "";
                    string logical = obj["NumberOfLogicalProcessors"]?.ToString() ?? "";
                    return name + cores + logical;
                }
            }
            catch { }
            return "";
        }

        private static string GetUsername()
        {
            try
            {
                return Environment.UserName;
            }
            catch
            {
                return "";
            }
        }

        private static string GetWindowsProductId()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (key != null)
                    {
                        var productId = key.GetValue("ProductId")?.ToString();
                        if (!string.IsNullOrEmpty(productId))
                            return productId;
                    }
                }
            }
            catch { }
            return "";
        }

        private static string GetVolumeSerial()
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID='C:'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    return obj["VolumeSerialNumber"]?.ToString() ?? "";
                }
            }
            catch { }
            return "";
        }

        private static string CreateHash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}