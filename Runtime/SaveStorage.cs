using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Atthree.Saving
{
    /// <summary>
    /// Kayıtların NEREYE yazılacağı. Varsayılan: <see cref="FileStorage"/> (diskteki dosyalar).
    /// Bulut kaydı, PlayerPrefs ya da test için başka bir depolama yazmak istersen bunu uygula.
    /// </summary>
    public interface ISaveStorage
    {
        void Write(string name, byte[] bytes);
        bool TryRead(string name, out byte[] bytes);
        /// <summary>Son başarılı yazmadan önceki sürüm (ana dosya bozulursa).</summary>
        bool TryReadBackup(string name, out byte[] bytes);
        bool Exists(string name);
        void Delete(string name);
        IEnumerable<string> List();
        /// <summary>İnsanlar için: dosyaların bulunduğu yer (yoksa null).</summary>
        string Location { get; }
    }

    /// <summary>
    /// Kayıtları <c>Application.persistentDataPath/Saves</c> altına dosya olarak yazar.
    ///
    /// Çökmeye karşı güvenli yazma:
    ///   1) Yeni içerik önce "isim.tmp" dosyasına yazılır.
    ///   2) Eski dosya "isim.bak" olarak saklanır, .tmp asıl dosyanın yerine geçer.
    /// Oyun 1. adımda çökerse eski kayıt sağlamdır; 2. adımdan sonra çökerse .bak'ten kurtarılır.
    /// </summary>
    public class FileStorage : ISaveStorage
    {
        public const string Extension = ".sav";
        readonly string root;

        public FileStorage() : this(Path.Combine(Application.persistentDataPath, "Saves")) { }

        public FileStorage(string rootFolder)
        {
            root = rootFolder;
        }

        public string Location => root;

        string PathOf(string name) => Path.Combine(root, name + Extension);

        public void Write(string name, byte[] bytes)
        {
            Directory.CreateDirectory(root);
            string path = PathOf(name);
            string temp = path + ".tmp";
            string backup = path + ".bak";

            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path))
            {
                // Atomik değiştir: asıl dosya .bak olur, .tmp asıl dosya olur.
                File.Replace(temp, path, backup, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        public bool TryRead(string name, out byte[] bytes) => TryReadFile(PathOf(name), out bytes);

        public bool TryReadBackup(string name, out byte[] bytes) => TryReadFile(PathOf(name) + ".bak", out bytes);

        static bool TryReadFile(string path, out byte[] bytes)
        {
            bytes = null;
            if (!File.Exists(path)) return false;
            try
            {
                bytes = File.ReadAllBytes(path);
                return true;
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[SaveSystem] Okunamadı: {path}\n{e.Message}");
                return false;
            }
        }

        public bool Exists(string name) => File.Exists(PathOf(name));

        public void Delete(string name)
        {
            string path = PathOf(name);
            foreach (var p in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }

        public IEnumerable<string> List()
        {
            if (!Directory.Exists(root)) return Enumerable.Empty<string>();
            return Directory.GetFiles(root, "*" + Extension)
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n);
        }
    }

    /// <summary>Sadece bellekte tutar (testler, ya da hiç diske yazmak istemediğin durumlar).</summary>
    public class MemoryStorage : ISaveStorage
    {
        readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
        readonly Dictionary<string, byte[]> backups = new Dictionary<string, byte[]>();

        public string Location => null;

        public void Write(string name, byte[] bytes)
        {
            if (files.TryGetValue(name, out var old)) backups[name] = old;
            files[name] = (byte[])bytes.Clone();
        }

        public bool TryRead(string name, out byte[] bytes) => files.TryGetValue(name, out bytes);
        public bool TryReadBackup(string name, out byte[] bytes) => backups.TryGetValue(name, out bytes);
        public bool Exists(string name) => files.ContainsKey(name);
        public void Delete(string name) { files.Remove(name); backups.Remove(name); }
        public IEnumerable<string> List() => files.Keys.OrderBy(n => n).ToList();

        /// <summary>Test için: dosyanın içeriğini bozar.</summary>
        public void Corrupt(string name) => files[name] = new byte[] { 1, 2, 3 };
    }
}
