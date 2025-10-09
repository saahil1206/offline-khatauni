using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OfflineOps
{
    public class LicenseManager
    {
        private const string LICENSE_KEY = "OfflineOps";
        private const string REGISTRY_PATH = @"SOFTWARE\OfflineOps";

        public static bool IsActivated()
        {
            string storedLicense = RetrieveLicense();
            if (string.IsNullOrEmpty(storedLicense))
                return false;

            // Verify the license is valid for this machine
            return ValidateLicense(storedLicense);
        }

        public static bool Activate(string username, string access_key)
        {
            // Get machine fingerprint
            string machineId = MachineFingerprint.GetMachineId();
            // Create encrypted license
            string license = CreateLicense(username, machineId, access_key);

            // Store in registry
            return StoreLicense(license);
        }

        public static bool DeActivate()
        {
            return StoreLicense(string.Empty);
        }

        private static string CreateLicense(string username, string machineId, string access_key)
        {
            string data = username + "|" + machineId + "|" + access_key;
            StaticVar.key = access_key;
            return EncryptString(data);
        }

        private static bool StoreLicense(string license)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH);
                key.SetValue(LICENSE_KEY, license);
                key.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string RetrieveLicense()
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH);
                if (key == null)
                    return null;

                string license = key.GetValue(LICENSE_KEY) as string;
                key.Close();
                return license;
            }
            catch
            {
                return null;
            }
        }

        private static bool ValidateLicense(string encryptedLicense)
        {
            try
            {
                string currentMachineId = MachineFingerprint.GetMachineId();

                string decryptedLicense = DecryptString(encryptedLicense);
                string[] parts = decryptedLicense.Split('|');

                if (parts.Length != 3)
                    return false;

                string storedMachineId = parts[1];
                //string currentMachineId = MachineFingerprint.GetMachineId();

                // License is valid only if machine ID matches
                bool isValid = (storedMachineId == currentMachineId);
                if (isValid)
                {
                    StaticVar.key = parts[2];
                }
                return isValid;
            }
            catch
            {
                return false;
            }
        }

        private static string EncryptString(string plainText)
        {
            byte[] key, iv; GenerateKeyAndIVSecure(out key, out iv);

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
        }

        private static string DecryptString(string cipherText)
        {
            byte[] key, iv; GenerateKeyAndIVSecure(out key, out iv);

            byte[] buffer = Convert.FromBase64String(cipherText);

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (MemoryStream msDecrypt = new MemoryStream(buffer))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }

        public static void GenerateKeyAndIVSecure(out byte[] key, out byte[] iv)
        {
            string machineId = MachineFingerprint.GetMachineId();
            using (var pbkdf2 = new Rfc2898DeriveBytes(machineId, Encoding.UTF8.GetBytes(LICENSE_KEY), 10000, HashAlgorithmName.SHA256))
            {
                key = pbkdf2.GetBytes(16);
                iv = new byte[16]; // IV will be generated/extracted separately
            }
        }
    }
}