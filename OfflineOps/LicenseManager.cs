using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
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
        private const string LICENSE_KEY = "OfflineOpsaa";
        private const string REGISTRY_PATH = @"SOFTWARE\OfflineOps";

        public static bool IsActivated()
        {
            string storedLicense = RetrieveLicense();
            if (string.IsNullOrEmpty(storedLicense))
                return false;

            // Verify the license is valid for this machine
            SQLiteCommand cmd = new SQLiteCommand("SELECT access_token, refresh_token FROM login_token"); cmd.CommandType = CommandType.Text;
            DatabaseHelper databaseHelper = new DatabaseHelper(); DataTable dt = databaseHelper.Read(cmd);
            if (dt.Rows.Count > 0)
            {
                StaticVar.access_token = dt.Rows[0]["access_token"].ToString();
                StaticVar.refresh_token = dt.Rows[0]["refresh_token"].ToString();
                return ValidateLicense(storedLicense);
            }
            return false;
        }

        public static bool Activate(string username, string access_token, string refresh_token)
        {
            // Get machine fingerprint
            string machineId = MachineFingerprint.GetMachineId();
            // Create encrypted license
            string license = CreateLicense(username, machineId, username);

            SQLiteCommand cmd = new SQLiteCommand("SELECT 1 FROM login_token"); cmd.CommandType = CommandType.Text;
            DatabaseHelper databaseHelper = new DatabaseHelper(); DataTable dt = databaseHelper.Read(cmd);
            int i = 0;
            if (dt.Rows.Count > 0)
            {
                cmd = new SQLiteCommand("UPDATE login_token SET access_token = @access_token, refresh_token = @refresh_token"); cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@access_token", DbType.String, -1).Value = access_token;
                cmd.Parameters.Add("@refresh_token", DbType.String, -1).Value = refresh_token;
                i = databaseHelper.Update(cmd);
            }
            else
            {
                cmd = new SQLiteCommand("INSERT INTO login_token(access_token, refresh_token)VALUES(@access_token, @refresh_token)"); cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@access_token", DbType.String, -1).Value = access_token;
                cmd.Parameters.Add("@refresh_token", DbType.String, -1).Value = refresh_token;
                i = databaseHelper.Update(cmd);
            }
            // Store in registry
            return StoreLicense(license) && i > 0;
        }

        public static bool UpdateTokens(string access_token, string refresh_token)
        {
            SQLiteCommand cmd = new SQLiteCommand("SELECT 1 FROM login_token"); cmd.CommandType = CommandType.Text;
            DatabaseHelper databaseHelper = new DatabaseHelper(); DataTable dt = databaseHelper.Read(cmd);
            int i = 0;
            if (dt.Rows.Count > 0)
            {
                cmd = new SQLiteCommand("UPDATE login_token SET access_token = @access_token, refresh_token = @refresh_token"); cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@access_token", DbType.String, -1).Value = access_token;
                cmd.Parameters.Add("@refresh_token", DbType.String, -1).Value = refresh_token;
                i = databaseHelper.Update(cmd);
            }
            else
            {
                cmd = new SQLiteCommand("INSERT INTO login_token(access_token, refresh_token)VALUES(@access_token, @refresh_token)"); cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add("@access_token", DbType.String, -1).Value = access_token;
                cmd.Parameters.Add("@refresh_token", DbType.String, -1).Value = refresh_token;
                i = databaseHelper.Update(cmd);
            }
            return i > 0;
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