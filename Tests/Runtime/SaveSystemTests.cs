using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Atthree.Saving.Tests
{
    public class SaveSystemTests
    {
        // Testte kullanılan kendi kaydedilebilir bileşenimiz.
        public class TestInventory : SaveableBehaviour<TestInventory.State>
        {
            public class State { public List<string> items = new List<string>(); public int gold; }
            public List<string> items = new List<string>();
            public int gold;
            protected override State Capture() => new State { items = new List<string>(items), gold = gold };
            protected override void Restore(State s) { items = s.items; gold = s.gold; }
        }

        public class Profile
        {
            public string name;
            public int level;
            public List<Vector3> checkpoints = new List<Vector3>();
            public Dictionary<string, int> stats = new Dictionary<string, int>();
        }

        MemoryStorage memory;
        readonly List<GameObject> created = new List<GameObject>();
        string tempFolder;

        [SetUp]
        public void SetUp()
        {
            memory = new MemoryStorage();
            SaveSystem.Configure(new SaveSettings { storage = memory });
            SaveSystem.NewGame();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created) if (go != null) Object.Destroy(go);
            created.Clear();
            if (tempFolder != null && Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
            tempFolder = null;
            SaveSystem.Configure(new SaveSettings { storage = new MemoryStorage() });
        }

        GameObject Entity(string id, params Type[] components)
        {
            var go = new GameObject(id);
            created.Add(go);
            go.AddComponent<SaveableEntity>().SetId(id);
            foreach (var c in components) go.AddComponent(c);
            return go;
        }

        // ------------------------------------------------------------------ anahtar-değer
        [Test]
        public void KeyValue_AllTypes_RoundTrip()
        {
            var d = SaveSystem.Current;
            d.Set("int", 42);
            d.Set("float", 3.25f);
            d.Set("string", "Merhaba ğüşıöç");
            d.Set("bool", true);
            d.Set("vector", new Vector3(1.5f, -2f, 3f));
            d.Set("rotation", Quaternion.Euler(10f, 20f, 30f));
            d.Set("color", new Color(0.1f, 0.2f, 0.3f, 0.4f));
            d.Set("list", new List<string> { "kılıç", "iksir" });
            d.Set("profile", new Profile
            {
                name = "Abdulkadir", level = 7,
                checkpoints = { new Vector3(1, 2, 3), new Vector3(4, 5, 6) },
                stats = { ["str"] = 12, ["dex"] = 9 },
            });

            SaveSystem.Save("slot1");
            SaveSystem.NewGame();
            Assert.IsFalse(SaveSystem.Current.Has("int"), "NewGame temizlemeli");
            Assert.IsTrue(SaveSystem.Load("slot1"));

            d = SaveSystem.Current;
            Assert.AreEqual(42, d.Get<int>("int"));
            Assert.AreEqual(3.25f, d.Get<float>("float"));
            Assert.AreEqual("Merhaba ğüşıöç", d.Get<string>("string"));
            Assert.IsTrue(d.Get<bool>("bool"));
            Assert.AreEqual(new Vector3(1.5f, -2f, 3f), d.Get<Vector3>("vector"));
            Assert.Less(Quaternion.Angle(Quaternion.Euler(10f, 20f, 30f), d.Get<Quaternion>("rotation")), 0.01f);
            Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f, 0.4f), d.Get<Color>("color"));
            CollectionAssert.AreEqual(new[] { "kılıç", "iksir" }, d.Get<List<string>>("list"));
            var p = d.Get<Profile>("profile");
            Assert.AreEqual("Abdulkadir", p.name);
            Assert.AreEqual(7, p.level);
            Assert.AreEqual(new Vector3(4, 5, 6), p.checkpoints[1]);
            Assert.AreEqual(9, p.stats["dex"]);
        }

        [Test]
        public void Get_ReturnsDefault_WhenMissingOrWrongType()
        {
            SaveSystem.Current.Set("name", "metin");
            Assert.AreEqual(5, SaveSystem.Current.Get("missing", 5));
            LogAssert.Expect(LogType.Warning, new Regex("'name' okunamadı"));
            Assert.AreEqual(-1, SaveSystem.Current.Get("name", -1));
        }

        [Test]
        public void InvalidSlotName_Throws()
        {
            Assert.Throws<ArgumentException>(() => SaveSystem.Save("../hack"));
            Assert.Throws<ArgumentException>(() => SaveSystem.Save(""));
            Assert.Throws<ArgumentException>(() => SaveSystem.Save("_preferences"));
        }

        // ------------------------------------------------------------------ sahne objeleri
        [UnityTest]
        public IEnumerator Entities_TransformActiveAndCustom_AreRestored()
        {
            var crate = Entity("crate", typeof(Rigidbody), typeof(SaveTransform));
            var coin = Entity("coin", typeof(SaveActiveState));
            var chest = Entity("chest", typeof(TestInventory));

            crate.transform.SetPositionAndRotation(new Vector3(1, 5, 2), Quaternion.Euler(0, 45, 0));
            var body = crate.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = new Vector3(0, 0, 3);
            chest.GetComponent<TestInventory>().items.Add("anahtar");
            chest.GetComponent<TestInventory>().gold = 50;
            yield return new WaitForFixedUpdate();

            SaveSystem.Save("slot1");
            Vector3 savedPos = crate.transform.position;

            // Her şeyi değiştir.
            yield return new WaitForSeconds(0.3f);
            body.linearVelocity = Vector3.zero;
            crate.transform.position = new Vector3(-10, 0, 0);
            coin.SetActive(false);
            chest.GetComponent<TestInventory>().items.Clear();
            chest.GetComponent<TestInventory>().gold = 0;

            // Pasif obje de geri yüklenmeli: önce kaydedilmiş hali aktifti.
            Assert.IsTrue(SaveSystem.Load("slot1"));
            Assert.Less(Vector3.Distance(savedPos, crate.transform.position), 0.001f, "konum geri gelmeli");
            Assert.AreEqual(45f, crate.transform.eulerAngles.y, 0.1f);
            Assert.AreEqual(3f, body.linearVelocity.z, 0.01f, "hız geri gelmeli");
            Assert.IsTrue(coin.activeSelf, "pasif obje aktif hale dönmeli");
            CollectionAssert.AreEqual(new[] { "anahtar" }, chest.GetComponent<TestInventory>().items);
            Assert.AreEqual(50, chest.GetComponent<TestInventory>().gold);
        }

        [UnityTest]
        public IEnumerator Entity_WithCharacterController_IsTeleportedBack()
        {
            var player = Entity("player", typeof(CharacterController), typeof(SaveTransform));
            var controller = player.GetComponent<CharacterController>();
            player.transform.position = new Vector3(3, 1, 3);
            yield return null;
            SaveSystem.Save("slot1");

            controller.Move(new Vector3(-8, 0, 0)); // oyuncu yürüdü
            yield return null;
            Assert.IsTrue(SaveSystem.Load("slot1"));
            yield return null;
            controller.Move(Vector3.zero); // controller bir sonraki hareketinde eski yere dönmemeli
            Assert.Less(Vector3.Distance(new Vector3(3, 1, 3), player.transform.position), 0.05f);
            Assert.IsTrue(controller.enabled, "controller tekrar açılmalı");
        }

        [Test]
        public void Entities_NotInScene_ArePreservedAcrossSaves()
        {
            // Sahne A'daki obje kaydedilir, sonra sahne değişir (obje yok), tekrar kaydedilir.
            var door = Entity("door", typeof(SaveTransform));
            door.transform.position = new Vector3(7, 0, 0);
            SaveSystem.Save("slot1");
            Object.DestroyImmediate(door);
            created.Remove(door);

            SaveSystem.Current.Set("note", "başka sahnedeyiz");
            SaveSystem.Save("slot1");

            // Sahne A tekrar yüklendi: kapı yeni yaratıldı, kaydedilen konumu almalı.
            SaveSystem.NewGame();
            var door2 = Entity("door", typeof(SaveTransform));
            Assert.IsTrue(SaveSystem.Load("slot1"));
            Assert.AreEqual(new Vector3(7, 0, 0), door2.transform.position);
        }

        [Test]
        public void Entity_WithoutId_IsSkippedWithWarning()
        {
            var go = new GameObject("no id");
            created.Add(go);
            go.AddComponent<SaveableEntity>();
            go.AddComponent<SaveTransform>();
            LogAssert.Expect(LogType.Warning, new Regex("id'si yok"));
            SaveSystem.Save("slot1");
        }

        // ------------------------------------------------------------------ dosya güvenliği
        [Test]
        public void FileStorage_WritesAtomically_AndRecoversFromBackup()
        {
            tempFolder = Path.Combine(Path.GetTempPath(), "SaveSystemTests_" + Guid.NewGuid().ToString("N"));
            SaveSystem.Configure(new SaveSettings { storage = new FileStorage(tempFolder) });

            SaveSystem.Current.Set("coins", 10);
            SaveSystem.Save("slot1");
            SaveSystem.Current.Set("coins", 20);
            SaveSystem.Save("slot1");

            string main = Path.Combine(tempFolder, "slot1.sav");
            Assert.IsTrue(File.Exists(main));
            Assert.IsTrue(File.Exists(main + ".bak"), "önceki sürüm yedeklenmeli");
            Assert.IsFalse(File.Exists(main + ".tmp"), "geçici dosya kalmamalı");

            // Yazma sırasında çökmüş gibi: asıl dosya yarım kalsın.
            File.WriteAllText(main, "{ \"meta\": { \"slot\": \"slo");
            LogAssert.Expect(LogType.Warning, new Regex("yedekten"));
            Assert.IsTrue(SaveSystem.Load("slot1"), "yedekten yüklenmeli");
            Assert.AreEqual(10, SaveSystem.Current.Get<int>("coins"), "yedek, bir önceki kayıttır");
        }

        [Test]
        public void CorruptSave_WithoutBackup_ReturnsFalse_AndKeepsCurrentGame()
        {
            SaveSystem.Current.Set("coins", 99);
            SaveSystem.Save("slot1");
            memory.Corrupt("slot1");
            SaveSystem.Current.Set("coins", 5);

            LogAssert.Expect(LogType.Error, new Regex("okunamadı"));
            Assert.IsFalse(SaveSystem.Load("slot1"));
            Assert.AreEqual(5, SaveSystem.Current.Get<int>("coins"), "başarısız yükleme oyunu bozmamalı");
        }

        [Test]
        public void MissingSave_ReturnsFalse_Silently()
        {
            Assert.IsFalse(SaveSystem.Exists("nothing"));
            Assert.IsFalse(SaveSystem.Load("nothing"));
        }

        // ------------------------------------------------------------------ sıkıştırma / şifreleme
        [Test]
        public void CompressionAndEncryption_RoundTrip_AndHideContent()
        {
            SaveSystem.Settings.compress = true;
            SaveSystem.Settings.encryptionKey = "gizli-anahtar";
            SaveSystem.Current.Set("secretValue", "çok gizli metin");
            SaveSystem.Save("slot1");

            Assert.IsTrue(memory.TryRead("slot1", out var bytes));
            Assert.IsFalse(Encoding.UTF8.GetString(bytes).Contains("secretValue"), "içerik okunamamalı");

            // Ayarı kapatsan bile eski sıkıştırılmış dosya açılır (başlıkta yazıyor).
            SaveSystem.Settings.compress = false;
            SaveSystem.NewGame();
            Assert.IsTrue(SaveSystem.Load("slot1"));
            Assert.AreEqual("çok gizli metin", SaveSystem.Current.Get<string>("secretValue"));

            // Yanlış anahtar: yüklenmemeli.
            SaveSystem.Settings.encryptionKey = "yanlis";
            LogAssert.Expect(LogType.Error, new Regex("okunamadı"));
            Assert.IsFalse(SaveSystem.Load("slot1"));
        }

        [Test]
        public void PlainSave_IsReadableJson()
        {
            SaveSystem.Current.Set("coins", 3);
            SaveSystem.Save("slot1");
            Assert.IsTrue(memory.TryRead("slot1", out var bytes));
            string text = Encoding.UTF8.GetString(bytes);
            StringAssert.Contains("\"coins\": 3", text);
        }

        // ------------------------------------------------------------------ sürüm geçişi
        [Test]
        public void Migration_UpgradesOldSaves()
        {
            // Sürüm 1: altın "gold" anahtarında.
            SaveSystem.Settings.version = 1;
            SaveSystem.Current.Set("gold", 120);
            SaveSystem.Save("slot1");

            // Sürüm 2: anahtarın adı "coins" oldu. Eski kayıtlar dönüştürülmeli.
            var settings = new SaveSettings { storage = memory, version = 2 };
            settings.AddMigration(1, root =>
            {
                var data = (Newtonsoft.Json.Linq.JObject)root["data"];
                data["coins"] = data["gold"];
                data.Remove("gold");
            });
            SaveSystem.Configure(settings);
            SaveSystem.NewGame();

            Assert.IsTrue(SaveSystem.Load("slot1"));
            Assert.AreEqual(120, SaveSystem.Current.Get<int>("coins"));
            Assert.IsFalse(SaveSystem.Current.Has("gold"));
            Assert.AreEqual(2, SaveSystem.CurrentMetadata.version);
        }

        // ------------------------------------------------------------------ slotlar
        [UnityTest]
        public IEnumerator Slots_ListNewestFirst_WithMetadata_AndDelete()
        {
            SaveSystem.Save("slot1", "Başlangıç");
            yield return new WaitForSecondsRealtime(1.1f); // zaman damgaları farklı olsun
            SaveSystem.Save("slot2", "Orman");

            var slots = SaveSystem.ListSlots();
            CollectionAssert.AreEqual(new[] { "slot2", "slot1" }, slots.Select(s => s.slot).ToArray(), "en yeni önce");
            Assert.AreEqual("Orman", slots[0].description);
            Assert.Greater(slots[0].playTimeSeconds, slots[1].playTimeSeconds, "oynama süresi artmalı");
            Assert.AreEqual(1, slots[0].version);
            Assert.IsFalse(string.IsNullOrEmpty(slots[0].scene));

            SaveSystem.Delete("slot1");
            Assert.IsFalse(SaveSystem.Exists("slot1"));
            Assert.AreEqual(1, SaveSystem.ListSlots().Count);
        }

        [Test]
        public void PlayTime_ContinuesFromLoadedSave()
        {
            SaveSystem.Save("slot1");
            Assert.IsTrue(memory.TryRead("slot1", out _));
            // Kayıttaki oynama süresini 1 saat yap.
            var root = Newtonsoft.Json.Linq.JObject.Parse(SaveSystem.ReadRawJson("slot1"));
            root["meta"]["playTimeSeconds"] = 3600;
            memory.Write("slot1", Encoding.UTF8.GetBytes(root.ToString()));

            Assert.IsTrue(SaveSystem.Load("slot1"));
            Assert.That(SaveSystem.PlayTimeSeconds, Is.InRange(3600, 3605));
        }

        // ------------------------------------------------------------------ ayarlar
        [Test]
        public void Preferences_AreIndependentFromSlots()
        {
            SaveSystem.Preferences.Set("volume", 0.4f);
            SaveSystem.SavePreferences();

            SaveSystem.Configure(new SaveSettings { storage = memory }); // yeniden açılış gibi
            SaveSystem.NewGame();
            Assert.AreEqual(0.4f, SaveSystem.Preferences.Get("volume", 1f));
            Assert.AreEqual(0, SaveSystem.ListSlots().Count, "ayarlar dosyası slot olarak görünmemeli");
        }
    }
}
