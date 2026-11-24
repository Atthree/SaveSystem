# Save System

**Save/load system for Unity 6**: key-value data, automatic saving of scene objects, save slots with
metadata, crash-safe files with backups, optional compression and encryption, and version migrations.

[English](#english) · [Türkçe](#türkçe)

---

<a name="english"></a>
# English

- **Key-value data:** `SaveSystem.Current.Set("coins", 120)`. Works with any type: numbers, strings,
  `Vector3`, `Quaternion`, `Color`, lists, dictionaries, your own classes.
- **Scene objects save themselves:** add `SaveableEntity` + `SaveTransform` to a crate and its position
  is saved. Write your own saveable component in a few lines.
- **Slots** with description, scene, date and play time, for a load menu.
- **Crash-safe:** files are written to a temporary file first, the previous version is kept as a
  backup, and a broken save is recovered from the backup automatically.
- **Optional compression (GZip) and encryption (AES).**
- **Version migrations:** when your save format changes, old saves are upgraded instead of broken.
- **Preferences:** a separate file for settings (volume, graphics) that isn't tied to a save slot.
- **Auto save**, an editor **Save Browser**, and a demo scene.

**Requirements:** Unity 6 (6000.0+). Uses Unity's official Newtonsoft JSON package (installed
automatically).

## Step-by-step setup

### 1. Add the package

**Window > Package Manager** → **+** → **Install package from git URL…** →

```
https://github.com/Atthree/SaveSystem.git
```

### 2. Try the demo

1. **Tools > Save System > Create Demo Scene**, then **Play**.
2. Press **Space** to throw the crates around, click the **gold coins** to collect them and the
   **sphere** to change its color.
3. **F5** quick-saves to `slot1`, **F9** loads it. The panel on the left has three slots with
   Save / Load / Delete and lists your saves with their date and play time.
4. Stop and Play again: the panel shows how many times the game was launched. That number is stored in
   **Preferences**, which survive across slots.
5. **Tools > Save System > Save Browser** shows every save file and its JSON content.

### 3. Save your own data

Anything that isn't a scene object (coins, unlocked levels, quest progress) goes into
`SaveSystem.Current`:

```csharp
using Atthree.Saving;

SaveSystem.Current.Set("coins", 120);
SaveSystem.Current.Set("unlockedLevels", new List<int> { 1, 2, 3 });

int coins = SaveSystem.Current.Get("coins", 0);   // 0 if missing

SaveSystem.Save("slot1", "Forest entrance");      // write to disk
SaveSystem.Load("slot1");                          // returns false if missing or unreadable
```

### 4. Make scene objects save themselves

1. Select the object (a crate, a door, an enemy).
2. **Add Component > Save System > Saveable Entity**. It gets a unique id automatically; a duplicated
   object (Ctrl+D) gets a new one.
3. Add what should be saved:
   - **Save Transform**: position, rotation (optionally scale) and Rigidbody velocity.
   - **Save Active State**: whether the object is active (collected coins, opened doors, dead enemies).
   - Your own component (below).

`SaveSystem.Save` captures every `SaveableEntity` in the loaded scenes; `SaveSystem.Load` restores them.

**Your own saveable component:**

```csharp
using Atthree.Saving;

public class Chest : SaveableBehaviour<Chest.State>
{
    public struct State { public bool opened; public int gold; }

    bool opened;
    int gold = 50;

    protected override State Capture() => new State { opened = opened, gold = gold };
    protected override void Restore(State s) { opened = s.opened; gold = s.gold; }
}
```

Add it next to a `SaveableEntity` and you're done. The public fields of `State` are saved.

### 5. A load menu

```csharp
foreach (SaveMetadata save in SaveSystem.ListSlots())   // newest first
{
    Debug.Log($"{save.slot}: {save.description} — {save.scene}, " +
              $"{save.SavedAtLocal:g}, played {save.PlayTime:hh\\:mm}");
}
SaveSystem.LoadWithScene("slot2");   // opens the scene the save was made in, then restores
```

`LoadWithScene` needs the scene to be in **File > Build Profiles** (scene list).

### 6. Settings (Preferences)

```csharp
SaveSystem.Preferences.Set("musicVolume", 0.7f);
SaveSystem.SavePreferences();
float volume = SaveSystem.Preferences.Get("musicVolume", 1f);
```

Preferences are one file, separate from slots: starting a new game or loading another slot doesn't
change them.

### 7. Auto save

Add **Save System > Auto Saver** to any object in the scene. It saves every N seconds, when the game
quits and when a mobile app is sent to the background. **Load On Start** loads that slot on startup.

## Settings

Configure once at startup, e.g. in the `Awake` of a script in your first scene:

```csharp
SaveSystem.Settings.compress = true;            // ~5-10x smaller files
SaveSystem.Settings.encryptionKey = "my-key";   // AES; changing it makes old saves unreadable
SaveSystem.Settings.prettyPrint = false;        // compact JSON for release builds
```

| Setting | Default | What it does |
|---|---|---|
| `compress` | off | GZip compression |
| `encryptionKey` | empty | AES-256 encryption when not empty |
| `prettyPrint` | on | Indented, human-readable JSON |
| `version` | 1 | Save format version (see migrations) |
| `useBackups` | on | Recover from `.bak` when the main file is broken |
| `storage` | `FileStorage` | Where files go. Implement `ISaveStorage` for cloud saves etc. |

Files are in `Application.persistentDataPath/Saves` (**Tools > Save System > Open Save Folder**).
Files remember how they were written, so turning compression or encryption on later doesn't break
existing saves (but changing the encryption key does).

> **About encryption:** the key is inside your game, so a determined player can find it. It stops
> casual editing of save files in a text editor; it is not real security.

## Version migrations

When you change what you save (rename a key, restructure data), raise the version and describe how to
convert old saves:

```csharp
SaveSystem.Settings.version = 2;
SaveSystem.Settings.AddMigration(1, root =>   // runs on saves made with version 1
{
    var data = (JObject)root["data"];
    data["coins"] = data["gold"];   // "gold" was renamed to "coins"
    data.Remove("gold");
});
```

`root` is the whole save (`meta`, `data`, `entities`) as a Newtonsoft `JObject`. Migrations run in
order: a version 1 save is upgraded 1 → 2 → 3.

## Good to know

- **Objects spawned at runtime** (dropped items, spawned enemies) have no id. Give them one with
  `GetComponent<SaveableEntity>().SetId("enemy_" + index)` before saving, and create them again before
  loading. Automatic re-spawning is not included yet.
- **Several scenes:** objects that aren't in the loaded scene keep their saved state; saving in
  scene B doesn't forget the objects of scene A.
- **`SaveSystem.BeforeSave`** is the right moment to copy values into `SaveSystem.Current`;
  **`SaveSystem.AfterLoad`** to read them back.
- **`SaveSystem.NewGame()`** clears the data in memory (not the files on disk).

## Files

| File | Job |
|---|---|
| `Runtime/SaveSystem.cs` | The API: Save, Load, slots, Preferences, migrations |
| `Runtime/SaveData.cs` | Key-value container (`Current`, `Preferences`) |
| `Runtime/SaveSettings.cs` | Settings |
| `Runtime/SaveStorage.cs` | `ISaveStorage`, `FileStorage` (atomic writes + backup), `MemoryStorage` |
| `Runtime/SaveEncoding.cs` | File format: header, GZip, AES |
| `Runtime/SaveableEntity.cs`, `Runtime/Saveable.cs` | Scene object id, `ISaveable`, `SaveableBehaviour<T>` |
| `Runtime/SaveTransform.cs`, `Runtime/SaveActiveState.cs` | Ready-made saveable components |
| `Runtime/AutoSaver.cs` | Auto save |
| `Runtime/UnityJsonConverters.cs` | JSON for Vector3, Quaternion, Color... |
| `Runtime/Demo/` | Demo panel and objects |
| `Editor/` | Save Browser, menus, SaveableEntity inspector |
| `Tests/Runtime/SaveSystemTests.cs` | 15 tests |

## Tests

15 PlayMode tests: all data types round-trip, defaults for missing values, invalid slot names, scene
objects (transform, active state, custom component, velocity, inactive objects), objects from other
scenes kept across saves, atomic writes and backup recovery, corrupt files, compression and
encryption (and wrong keys), migrations, slot listing and deleting, play time, Preferences.

```json
"testables": [ "com.atthree.save-system" ]
```

Add this to `Packages/manifest.json`, then **Window > General > Test Runner > PlayMode**.

## License

[MIT](LICENSE.md)

---

<a name="türkçe"></a>
# Türkçe

- **Anahtar-değer veri:** `SaveSystem.Current.Set("coins", 120)`. Her tiple çalışır: sayılar, metinler,
  `Vector3`, `Quaternion`, `Color`, listeler, sözlükler, kendi sınıfların.
- **Sahne objeleri kendini kaydeder:** bir kutuya `SaveableEntity` + `SaveTransform` ekle, konumu
  kaydedilir. Kendi kaydedilebilir bileşenini birkaç satırda yaz.
- **Slotlar:** açıklama, sahne, tarih ve oynama süresiyle; yükleme menüsü için.
- **Çökmeye karşı güvenli:** dosya önce geçici dosyaya yazılır, önceki sürüm yedek olarak saklanır,
  bozuk kayıt otomatik olarak yedekten kurtarılır.
- **İsteğe bağlı sıkıştırma (GZip) ve şifreleme (AES).**
- **Sürüm geçişi:** kayıt formatın değişince eski kayıtlar bozulmaz, yeni formata çevrilir.
- **Preferences:** ayarlar (ses, grafik) için kayıt slotlarından bağımsız ayrı bir dosya.
- **Otomatik kayıt**, editörde **Save Browser** ve bir demo sahnesi.

**Gereksinimler:** Unity 6 (6000.0+). Unity'nin resmi Newtonsoft JSON paketini kullanır (otomatik kurulur).

## Adım adım kurulum

### 1. Paketi ekle

**Window > Package Manager** → **+** → **Install package from git URL…** →

```
https://github.com/Atthree/SaveSystem.git
```

### 2. Demoyu dene

1. **Tools > Save System > Create Demo Scene**, sonra **Play**.
2. **Boşluk** ile kutuları savur, **altınlara** tıklayıp topla, **küreye** tıklayıp rengini değiştir.
3. **F5** `slot1`'e hızlı kaydeder, **F9** yükler. Soldaki panelde Kaydet / Yükle / Sil düğmeli üç
   slot ve tarih, oynama süresiyle kayıt listesi var.
4. Play'den çık ve tekrar gir: panel oyunun kaç kez açıldığını gösterir. Bu sayı slotlardan bağımsız
   **Preferences**'ta saklanır.
5. **Tools > Save System > Save Browser** tüm kayıt dosyalarını ve JSON içeriklerini gösterir.

### 3. Kendi verini kaydet

Sahne objesi olmayan her şey (altın, açılan bölümler, görev ilerlemesi) `SaveSystem.Current`'a gider:

```csharp
using Atthree.Saving;

SaveSystem.Current.Set("coins", 120);
SaveSystem.Current.Set("unlockedLevels", new List<int> { 1, 2, 3 });

int coins = SaveSystem.Current.Get("coins", 0);   // yoksa 0

SaveSystem.Save("slot1", "Orman girişi");          // diske yaz
SaveSystem.Load("slot1");                           // yoksa ya da okunamazsa false döner
```

### 4. Sahne objelerini kaydedilebilir yap

1. Objeyi seç (kutu, kapı, düşman).
2. **Add Component > Save System > Saveable Entity**. Otomatik benzersiz bir id alır; kopyalanan obje
   (Ctrl+D) yeni id alır.
3. Neyin kaydedileceğini ekle:
   - **Save Transform**: konum, rotasyon (istersen ölçek) ve Rigidbody hızı.
   - **Save Active State**: objenin açık/kapalı olması (toplanan altın, açılan kapı, ölen düşman).
   - Kendi bileşenin (aşağıda).

`SaveSystem.Save` yüklü sahnelerdeki tüm `SaveableEntity`'leri yakalar; `SaveSystem.Load` geri yükler.

**Kendi kaydedilebilir bileşenin:**

```csharp
using Atthree.Saving;

public class Chest : SaveableBehaviour<Chest.State>
{
    public struct State { public bool opened; public int gold; }

    bool opened;
    int gold = 50;

    protected override State Capture() => new State { opened = opened, gold = gold };
    protected override void Restore(State s) { opened = s.opened; gold = s.gold; }
}
```

Bir `SaveableEntity`'nin yanına ekle, bu kadar. `State`'in public alanları kaydedilir.

### 5. Yükleme menüsü

```csharp
foreach (SaveMetadata save in SaveSystem.ListSlots())   // en yeni önce
{
    Debug.Log($"{save.slot}: {save.description} — {save.scene}, " +
              $"{save.SavedAtLocal:g}, oynama {save.PlayTime:hh\\:mm}");
}
SaveSystem.LoadWithScene("slot2");   // kaydın yapıldığı sahneyi açar, sonra geri yükler
```

`LoadWithScene` için sahnenin **File > Build Profiles** sahne listesinde olması gerekir.

### 6. Ayarlar (Preferences)

```csharp
SaveSystem.Preferences.Set("musicVolume", 0.7f);
SaveSystem.SavePreferences();
float volume = SaveSystem.Preferences.Get("musicVolume", 1f);
```

Preferences slotlardan ayrı tek bir dosyadır: yeni oyun başlatmak ya da başka slot yüklemek onları
değiştirmez.

### 7. Otomatik kayıt

Sahnedeki herhangi bir objeye **Save System > Auto Saver** ekle. N saniyede bir, oyundan çıkarken ve
mobilde uygulama arka plana atılınca kaydeder. **Load On Start** açılışta o slotu yükler.

## Ayarlar

Oyun açılırken bir kez ayarla, ör. ilk sahnedeki bir script'in `Awake`'inde:

```csharp
SaveSystem.Settings.compress = true;             // dosyalar ~5-10 kat küçülür
SaveSystem.Settings.encryptionKey = "anahtarim"; // AES; değiştirirsen eski kayıtlar açılmaz
SaveSystem.Settings.prettyPrint = false;         // yayın sürümünde sıkışık JSON
```

| Ayar | Varsayılan | Ne yapar |
|---|---|---|
| `compress` | kapalı | GZip sıkıştırma |
| `encryptionKey` | boş | Boş değilse AES-256 şifreleme |
| `prettyPrint` | açık | Girintili, okunaklı JSON |
| `version` | 1 | Kayıt formatı sürümü (sürüm geçişine bak) |
| `useBackups` | açık | Ana dosya bozuksa `.bak`'ten kurtar |
| `storage` | `FileStorage` | Dosyaların yeri. Bulut kaydı vb. için `ISaveStorage` uygula |

Dosyalar `Application.persistentDataPath/Saves` içinde (**Tools > Save System > Open Save Folder**).
Dosyalar nasıl yazıldıklarını hatırlar; sıkıştırmayı ya da şifrelemeyi sonradan açmak eski kayıtları
bozmaz (ama şifreleme anahtarını değiştirmek bozar).

> **Şifreleme hakkında:** anahtar oyunun içinde durduğu için kararlı bir oyuncu onu bulabilir. Bu,
> kayıt dosyasının Not Defteri'yle kolayca değiştirilmesini engeller; gerçek güvenlik değildir.

## Sürüm geçişi (migration)

Kaydettiğin şeyleri değiştirdiğinde (anahtarın adını değiştirmek, veriyi yeniden düzenlemek) sürümü
artır ve eski kayıtların nasıl çevrileceğini yaz:

```csharp
SaveSystem.Settings.version = 2;
SaveSystem.Settings.AddMigration(1, root =>   // sürüm 1 ile yapılmış kayıtlarda çalışır
{
    var data = (JObject)root["data"];
    data["coins"] = data["gold"];   // "gold"un adı "coins" oldu
    data.Remove("gold");
});
```

`root` kaydın tamamıdır (`meta`, `data`, `entities`), Newtonsoft `JObject` olarak. Dönüşümler sırayla
çalışır: sürüm 1 kaydı 1 → 2 → 3 diye yükseltilir.

## Bilmekte fayda var

- **Oyun sırasında yaratılan objelerin** (düşen eşyalar, doğan düşmanlar) id'si yoktur. Kaydetmeden
  önce `GetComponent<SaveableEntity>().SetId("enemy_" + index)` ile id ver ve yüklemeden önce onları
  tekrar yarat. Otomatik yeniden yaratma henüz yok.
- **Birden çok sahne:** yüklü sahnede olmayan objeler kayıtlı durumlarını korur; B sahnesinde
  kaydetmek A sahnesinin objelerini unutmaz.
- **`SaveSystem.BeforeSave`** değerleri `SaveSystem.Current`'a yazmak için doğru andır;
  **`SaveSystem.AfterLoad`** geri okumak için.
- **`SaveSystem.NewGame()`** bellekteki veriyi temizler (diskteki dosyaları değil).

Dosya listesi ve testler için İngilizce bölümdeki [Files](#files) ve [Tests](#tests) kısımlarına bak.

## Lisans

[MIT](LICENSE.md)
