using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Atthree.Saving
{
    /// <summary>
    /// Kayıt sisteminin ana kapısı. Her yerden çağrılabilir (statik).
    ///
    ///   SaveSystem.Current.Set("coins", 120);   // anahtar-değer veri
    ///   SaveSystem.Save("slot1", "Orman girişi");  // + sahnedeki SaveableEntity'ler
    ///   SaveSystem.Load("slot1");
    ///   foreach (var meta in SaveSystem.ListSlots()) ...
    ///
    ///   SaveSystem.Preferences.Set("volume", 0.8f);  // ayarlar: slotlardan bağımsız tek dosya
    ///   SaveSystem.SavePreferences();
    /// </summary>
    public static class SaveSystem
    {
        public const string DefaultSlot = "default";
        const string MetaSuffix = ".meta";
        const string PreferencesName = "_preferences";

        public static SaveSettings Settings { get; private set; } = new SaveSettings();

        /// <summary>Şu anki oyunun anahtar-değer verisi. Save() bunu yazar, Load() bunu doldurur.</summary>
        public static SaveData Current { get; private set; } = new SaveData();

        /// <summary>Son kaydedilen ya da yüklenen slot (yeni oyunda null).</summary>
        public static string CurrentSlot { get; private set; }

        /// <summary>Son kaydedilen ya da yüklenen slotun özeti.</summary>
        public static SaveMetadata CurrentMetadata { get; private set; }

        /// <summary>Kaydetmeden hemen önce: son değerleri Current'a yazmak için iyi bir an.</summary>
        public static event Action<string> BeforeSave;
        public static event Action<string> AfterSave;
        /// <summary>Yükledikten (ve sahne objelerini geri yükledikten) sonra.</summary>
        public static event Action<string> AfterLoad;

        // Sahne objelerinin durumu. Sahnede olmayan objelerin durumu da burada kalır (başka sahneler).
        static Dictionary<string, Dictionary<string, JToken>> entityStates = new Dictionary<string, Dictionary<string, JToken>>();
        static double playTimeAtLoad;
        static double sessionStart;
        static SaveData preferences;

        /// <summary>Bu oyunun toplam oynama süresi (yüklenen kayıttaki süre + bu oturum).</summary>
        public static double PlayTimeSeconds => playTimeAtLoad + (Time.realtimeSinceStartupAsDouble - sessionStart);

        // Domain reload kapalıyken (Enter Play Mode Options) statik alanlar eski oyundan kalmasın.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Settings = new SaveSettings();
            BeforeSave = AfterSave = AfterLoad = null;
            preferences = null;
            NewGame();
        }

        /// <summary>Ayarları toptan değiştir (ör. testlerde farklı bir depolama ile).</summary>
        public static void Configure(SaveSettings settings)
        {
            Settings = settings ?? new SaveSettings();
            preferences = null;
        }

        /// <summary>Yeni oyun: bellekteki her şeyi temizler (diskteki kayıtlara dokunmaz).</summary>
        public static void NewGame()
        {
            Current = new SaveData();
            entityStates = new Dictionary<string, Dictionary<string, JToken>>();
            CurrentSlot = null;
            CurrentMetadata = null;
            playTimeAtLoad = 0;
            sessionStart = Time.realtimeSinceStartupAsDouble;
        }

        // ================================================================== KAYDET
        public static void Save(string slot = DefaultSlot, string description = null)
        {
            ValidateSlot(slot);
            BeforeSave?.Invoke(slot);

            CaptureEntities();

            var file = new SaveFile
            {
                meta = new SaveMetadata
                {
                    slot = slot,
                    description = description ?? CurrentMetadata?.description ?? "",
                    scene = SceneManager.GetActiveScene().name,
                    savedAtUtc = DateTime.UtcNow.ToString("o"),
                    playTimeSeconds = PlayTimeSeconds,
                    version = Settings.version,
                    gameVersion = Application.version,
                },
                data = Current.Values,
                entities = entityStates,
            };

            var formatting = Settings.prettyPrint ? Formatting.Indented : Formatting.None;
            string json = JsonConvert.SerializeObject(file, formatting, SaveSerializer.Settings);
            string metaJson = JsonConvert.SerializeObject(file.meta, Formatting.Indented);

            var storage = Settings.Storage;
            storage.Write(slot, SaveEncoding.Encode(json, Settings.compress, Settings.encryptionKey));
            storage.Write(slot + MetaSuffix, SaveEncoding.Encode(metaJson, false, null)); // özet hep düz JSON

            CurrentSlot = slot;
            CurrentMetadata = file.meta;
            AfterSave?.Invoke(slot);
        }

        // ================================================================== YÜKLE
        /// <summary>
        /// Kaydı belleğe alır ve şu an yüklü sahnelerdeki objeleri geri yükler.
        /// Kayıt yoksa ya da okunamıyorsa false döner (bellekteki oyun değişmez).
        /// </summary>
        public static bool Load(string slot = DefaultSlot)
        {
            ValidateSlot(slot);
            if (!TryReadSaveFile(slot, out var file)) return false;

            Current = new SaveData(file.data ?? new Dictionary<string, JToken>());
            entityStates = file.entities ?? new Dictionary<string, Dictionary<string, JToken>>();
            CurrentSlot = slot;
            CurrentMetadata = file.meta;
            playTimeAtLoad = file.meta?.playTimeSeconds ?? 0;
            sessionStart = Time.realtimeSinceStartupAsDouble;

            RestoreEntities();
            AfterLoad?.Invoke(slot);
            return true;
        }

        /// <summary>
        /// Kaydı yükler, kaydedildiği sahne şu anki sahne değilse önce o sahneyi açar
        /// (sahne Build Settings'te olmalı), sonra objeleri geri yükler.
        /// </summary>
        public static bool LoadWithScene(string slot = DefaultSlot)
        {
            ValidateSlot(slot);
            if (!TryReadSaveFile(slot, out var file)) return false;
            string scene = file.meta?.scene;
            if (string.IsNullOrEmpty(scene) || scene == SceneManager.GetActiveScene().name) return Load(slot);

            void OnLoaded(Scene s, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= OnLoaded;
                Load(slot);
            }
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.LoadScene(scene);
            return true;
        }

        /// <summary>
        /// Bellekteki kayıttan, şu an yüklü sahnelerdeki SaveableEntity'leri geri yükler.
        /// Yeni bir sahne açtıktan sonra (Load'u tekrar çağırmadan) kullanılabilir.
        /// </summary>
        public static void RestoreEntities()
        {
            foreach (var entity in FindEntities())
                if (entityStates.TryGetValue(entity.Id, out var state))
                    entity.RestoreState(state);
        }

        static void CaptureEntities()
        {
            var seen = new HashSet<string>();
            foreach (var entity in FindEntities())
            {
                if (!seen.Add(entity.Id))
                {
                    Debug.LogWarning($"[SaveSystem] Aynı id'ye sahip iki obje var: '{entity.Id}' ({entity.name}). " +
                                     "Biri diğerinin üzerine yazılır. Objenin SaveableEntity'sinde 'Yeni Id' kullan.", entity);
                }
                entityStates[entity.Id] = entity.CaptureState();
            }
        }

        static IEnumerable<SaveableEntity> FindEntities()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!entity.gameObject.scene.IsValid()) continue; // prefab asset'leri
                if (string.IsNullOrEmpty(entity.Id))
                {
                    Debug.LogWarning($"[SaveSystem] '{entity.name}' objesinin id'si yok, kaydedilmiyor. " +
                                     "Oyun sırasında yaratılan objelere SetId(...) ile id ver.", entity);
                    continue;
                }
                yield return entity;
            }
        }

        // ================================================================== DOSYA OKUMA
        static bool TryReadSaveFile(string slot, out SaveFile file)
        {
            file = null;
            var storage = Settings.Storage;
            if (!storage.TryRead(slot, out var bytes)) return false;

            if (TryParse(bytes, slot, out file, out string error)) return true;

            if (Settings.useBackups && storage.TryReadBackup(slot, out var backup) && TryParse(backup, slot, out file, out _))
            {
                Debug.LogWarning($"[SaveSystem] '{slot}' kaydı bozuktu ({error}); yedekten (.bak) kurtarıldı.");
                return true;
            }

            Debug.LogError($"[SaveSystem] '{slot}' kaydı okunamadı: {error}");
            return false;
        }

        static bool TryParse(byte[] bytes, string slot, out SaveFile file, out string error)
        {
            file = null;
            error = null;
            try
            {
                string json = SaveEncoding.Decode(bytes, Settings.encryptionKey);
                var root = JObject.Parse(json);
                Migrate(root, slot);
                file = root.ToObject<SaveFile>(SaveSerializer.Instance);
                if (file == null) { error = "boş kayıt"; return false; }
                return true;
            }
            catch (Exception e) // bozuk dosya her türlü hataya yol açabilir; hepsi "okunamadı" demek
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>Eski sürümdeki kaydı, eklenen dönüşümlerle şu anki sürüme getirir.</summary>
        static void Migrate(JObject root, string slot)
        {
            int version = root["meta"]?["version"]?.Value<int>() ?? 1;
            while (version < Settings.version)
            {
                if (Settings.TryGetMigration(version, out var migrate))
                    migrate(root);
                else
                    Debug.LogWarning($"[SaveSystem] '{slot}': sürüm {version} → {version + 1} için dönüşüm yok, olduğu gibi okunuyor.");
                version++;
            }
            if (root["meta"] is JObject meta) meta["version"] = version;
        }

        // ================================================================== SLOTLAR
        public static bool Exists(string slot = DefaultSlot) => Settings.Storage.Exists(slot);

        public static void Delete(string slot = DefaultSlot)
        {
            ValidateSlot(slot);
            Settings.Storage.Delete(slot);
            Settings.Storage.Delete(slot + MetaSuffix);
            if (CurrentSlot == slot) CurrentSlot = null;
        }

        /// <summary>Tüm kayıtların özeti, en yeni önce. Kayıt menüsü için.</summary>
        public static List<SaveMetadata> ListSlots()
        {
            var result = new List<SaveMetadata>();
            var storage = Settings.Storage;
            foreach (var name in storage.List())
            {
                if (name.EndsWith(MetaSuffix) || name.StartsWith("_")) continue;
                SaveMetadata meta = null;
                if (storage.TryRead(name + MetaSuffix, out var bytes))
                {
                    try { meta = JsonConvert.DeserializeObject<SaveMetadata>(SaveEncoding.Decode(bytes, null)); }
                    catch (Exception) { /* özet bozuksa asıl dosyadan oku */ }
                }
                if (meta == null && TryReadSaveFile(name, out var file)) meta = file.meta;
                if (meta == null) meta = new SaveMetadata { slot = name, description = "(okunamadı)" };
                meta.slot = name;
                result.Add(meta);
            }
            return result.OrderByDescending(m => m.SavedAtLocal).ToList();
        }

        /// <summary>Kaydın JSON metni (hata ayıklama, editördeki kayıt tarayıcısı için).</summary>
        public static string ReadRawJson(string slot)
        {
            if (!Settings.Storage.TryRead(slot, out var bytes)) return null;
            try { return JObject.Parse(SaveEncoding.Decode(bytes, Settings.encryptionKey)).ToString(Formatting.Indented); }
            catch (Exception e) { return "Okunamadı: " + e.Message; }
        }

        // ================================================================== AYARLAR (PREFERENCES)
        /// <summary>
        /// Oyuncu ayarları (ses, grafik, tuşlar...). Kayıt slotlarından bağımsızdır: yeni oyun ya da
        /// başka slot yüklemek bunları değiştirmez. İlk erişimde diskten okunur.
        /// </summary>
        public static SaveData Preferences
        {
            get
            {
                if (preferences != null) return preferences;
                preferences = new SaveData();
                if (Settings.Storage.TryRead(PreferencesName, out var bytes))
                {
                    try
                    {
                        var values = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(
                            SaveEncoding.Decode(bytes, Settings.encryptionKey), SaveSerializer.Settings);
                        if (values != null) preferences = new SaveData(values);
                    }
                    catch (Exception e) { Debug.LogWarning("[SaveSystem] Ayarlar okunamadı, varsayılanlar kullanılıyor: " + e.Message); }
                }
                return preferences;
            }
        }

        public static void SavePreferences()
        {
            string json = JsonConvert.SerializeObject(Preferences.Values, Formatting.Indented, SaveSerializer.Settings);
            Settings.Storage.Write(PreferencesName, SaveEncoding.Encode(json, Settings.compress, Settings.encryptionKey));
        }

        // ================================================================== yardımcılar
        static readonly Regex ValidSlot = new Regex(@"^[A-Za-z0-9][A-Za-z0-9_\- ]{0,63}$");

        static void ValidateSlot(string slot)
        {
            if (slot == null || !ValidSlot.IsMatch(slot) || slot.EndsWith(MetaSuffix))
                throw new ArgumentException($"Geçersiz slot adı: '{slot}'. Harf/rakam ile başlamalı; harf, rakam, boşluk, _ ve - içerebilir.");
        }
    }
}
