using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Atthree.Saving
{
    /// <summary>
    /// JSON metnini diske yazılacak byte'lara çevirir (ve tersi):
    ///
    ///   [ "ASV1" (4 byte) ][ bayraklar (1 byte) ][ içerik ]
    ///   bayrak 1 = sıkıştırılmış (GZip), bayrak 2 = şifreli (AES-256)
    ///
    /// Başlık sayesinde dosya, o anki ayarlardan bağımsız okunur: sıkıştırmayı sonradan açsan bile
    /// eski sıkıştırılmamış kayıtlar açılmaya devam eder. Başlığı olmayan dosya düz JSON sayılır.
    ///
    /// Şifreleme hakkında dürüst not: anahtar oyunun içinde durduğu için kararlı biri onu bulabilir.
    /// Bu, oyuncunun kaydı Not Defteri ile kolayca değiştirmesini engeller; gerçek güvenlik değildir.
    /// </summary>
    public static class SaveEncoding
    {
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("ASV1");
        const byte FlagCompressed = 1;
        const byte FlagEncrypted = 2;

        public static byte[] Encode(string json, bool compress, string encryptionKey)
        {
            byte[] content = Encoding.UTF8.GetBytes(json);
            byte flags = 0;

            if (compress)
            {
                content = GZip(content);
                flags |= FlagCompressed;
            }
            if (!string.IsNullOrEmpty(encryptionKey))
            {
                content = Encrypt(content, encryptionKey);
                flags |= FlagEncrypted;
            }
            if (flags == 0) return content; // düz JSON: elle okunabilir

            var result = new byte[Magic.Length + 1 + content.Length];
            Buffer.BlockCopy(Magic, 0, result, 0, Magic.Length);
            result[Magic.Length] = flags;
            Buffer.BlockCopy(content, 0, result, Magic.Length + 1, content.Length);
            return result;
        }

        /// <exception cref="SaveCorruptedException">Dosya okunamıyor, bozuk ya da anahtar yanlış.</exception>
        public static string Decode(byte[] bytes, string encryptionKey)
        {
            if (bytes == null || bytes.Length == 0) throw new SaveCorruptedException("Dosya boş.");
            if (!HasMagic(bytes)) return Encoding.UTF8.GetString(bytes);

            byte flags = bytes[Magic.Length];
            var content = new byte[bytes.Length - Magic.Length - 1];
            Buffer.BlockCopy(bytes, Magic.Length + 1, content, 0, content.Length);

            try
            {
                if ((flags & FlagEncrypted) != 0)
                {
                    if (string.IsNullOrEmpty(encryptionKey))
                        throw new SaveCorruptedException("Kayıt şifreli ama SaveSettings.encryptionKey boş.");
                    content = Decrypt(content, encryptionKey);
                }
                if ((flags & FlagCompressed) != 0) content = GUnzip(content);
                return Encoding.UTF8.GetString(content);
            }
            catch (CryptographicException e) { throw new SaveCorruptedException("Şifre çözülemedi (yanlış anahtar ya da bozuk dosya): " + e.Message); }
            catch (InvalidDataException e) { throw new SaveCorruptedException("Sıkıştırma açılamadı: " + e.Message); }
        }

        static bool HasMagic(byte[] bytes)
        {
            if (bytes.Length <= Magic.Length) return false;
            for (int i = 0; i < Magic.Length; i++) if (bytes[i] != Magic[i]) return false;
            return true;
        }

        static byte[] GZip(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal)) gzip.Write(data, 0, data.Length);
            return output.ToArray();
        }

        static byte[] GUnzip(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        // ---- AES-256-CBC. Anahtar, paroladan PBKDF2 ile türetilir. Çıktı: [salt 16][iv 16][şifreli]
        static byte[] Encrypt(byte[] data, string password)
        {
            byte[] salt = RandomBytes(16);
            using var aes = Aes.Create();
            aes.Key = DeriveKey(password, salt);
            aes.GenerateIV();
            using var encryptor = aes.CreateEncryptor();
            byte[] cipher = encryptor.TransformFinalBlock(data, 0, data.Length);

            var result = new byte[16 + 16 + cipher.Length];
            Buffer.BlockCopy(salt, 0, result, 0, 16);
            Buffer.BlockCopy(aes.IV, 0, result, 16, 16);
            Buffer.BlockCopy(cipher, 0, result, 32, cipher.Length);
            return result;
        }

        static byte[] Decrypt(byte[] data, string password)
        {
            if (data.Length < 33) throw new CryptographicException("Veri çok kısa.");
            var salt = new byte[16];
            var iv = new byte[16];
            Buffer.BlockCopy(data, 0, salt, 0, 16);
            Buffer.BlockCopy(data, 16, iv, 0, 16);

            using var aes = Aes.Create();
            aes.Key = DeriveKey(password, salt);
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(data, 32, data.Length - 32);
        }

        static byte[] DeriveKey(string password, byte[] salt)
        {
            using var kdf = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            return kdf.GetBytes(32);
        }

        static byte[] RandomBytes(int count)
        {
            var bytes = new byte[count];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes;
        }
    }

    public class SaveCorruptedException : Exception
    {
        public SaveCorruptedException(string message) : base(message) { }
    }
}
