using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Atthree.Saving
{
    /// <summary>
    /// Kayıt sisteminin ayarları. Oyun açılırken bir kez ayarla (ör. ilk sahnedeki bir script'in Awake'inde):
    ///
    ///   SaveSystem.Settings.compress = true;
    ///   SaveSystem.Settings.encryptionKey = "benim-gizli-anahtarim";
    ///   SaveSystem.Settings.version = 2;
    ///   SaveSystem.Settings.AddMigration(1, root => { ... });   // 1 → 2 dönüşümü
    /// </summary>
    [Serializable]
    public class SaveSettings
    {
        [Tooltip("Kayıtların yazıldığı yer. Boşsa persistentDataPath/Saves.")]
        [NonSerialized] public ISaveStorage storage;

        [Tooltip("GZip ile sıkıştır (dosya ~5-10 kat küçülür, elle okunamaz).")]
        public bool compress;

        [Tooltip("Boş değilse kayıtlar AES ile şifrelenir. Değiştirirsen eski kayıtlar açılmaz!")]
        public string encryptionKey = "";

        [Tooltip("JSON'u okunaklı (girintili) yaz. Geliştirirken açık, yayında kapalı tut.")]
        public bool prettyPrint = true;

        [Tooltip("Kayıt FORMATININ sürümü. Kayıt yapısını değiştirince artır ve bir dönüşüm (migration) ekle.")]
        public int version = 1;

        [Tooltip("Ana dosya bozuksa yedekten (.bak) yüklemeyi dene.")]
        public bool useBackups = true;

        readonly Dictionary<int, Action<JObject>> migrations = new Dictionary<int, Action<JObject>>();

        /// <summary>
        /// <paramref name="fromVersion"/> sürümündeki bir kaydı bir sonraki sürüme çeviren fonksiyon.
        /// Fonksiyon kaydın tamamını (JObject: meta, data, entities) değiştirebilir.
        /// </summary>
        public void AddMigration(int fromVersion, Action<JObject> migrate) => migrations[fromVersion] = migrate;

        internal bool TryGetMigration(int fromVersion, out Action<JObject> migrate) => migrations.TryGetValue(fromVersion, out migrate);

        /// <summary>Kullanılan depolama (ayarlanmamışsa varsayılan FileStorage oluşturulur).</summary>
        public ISaveStorage Storage => storage ??= new FileStorage();
    }
}
