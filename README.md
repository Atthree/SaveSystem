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

## Using your own models and prefabs

Characters, props and enemies from the Asset Store or your own Blender models are saved the same way.
The rules below decide whether they load back correctly.

### Setup checklist

| # | Do this | Why |
|---|---|---|
| 1 | Put `SaveableEntity` on the **root** object of the model (the one you move), not on a child mesh | `SaveTransform` saves the position of the object it is on |
| 2 | Add it to the **prefab** if every copy should be saved | The prefab asset itself gets no id; every copy placed in a scene gets its own id automatically |
| 3 | Place objects in the scene **in the Editor** whenever possible | They get a fixed id. Objects created by code at runtime need `SetId` (see below) |
| 4 | Add one saveable component per thing to save: `SaveTransform` (where), `SaveActiveState` (exists or not), your own component (health, inventory, open/closed) | Only components that implement `ISaveable` are saved; nothing is saved "by itself" |

### What works out of the box

| Model has | Result |
|---|---|
| `Rigidbody` | Position, rotation and velocity are restored |
| `CharacterController` (most character controllers) | Handled: it is disabled for a moment while the character is moved back, then enabled again |
| `NavMeshAgent` (AI enemies) | Handled: the agent is moved with `Warp`, so it doesn't snap back |
| Inactive object (collected item) | Found and restored, even though it's inactive |
| Child objects without their own `SaveableEntity` | They move with their parent; their own state isn't saved |

### What needs your attention

| Situation | What to do |
|---|---|
| **Animator** (a door's open animation, a character's pose) | Not saved. Save the *state* that drives it (e.g. `isOpen`) in your own component and set the Animator parameter in `Restore` |
| **Ragdolls / several Rigidbodies** under one model | Only the object with `SaveTransform` is restored. Add `SaveableEntity` + `SaveTransform` to each body part that must keep its pose, or save the character standing |
| **A script that moves the object every frame** (your own player controller) | If it stores its own position/target, restore those too in a custom component, otherwise it moves the object back |
| **Objects created by code** (`Instantiate`) | They copy the prefab's empty id, or the id of the object you cloned. Give each one a stable id: `SetId("enemy_" + index)`, and create them again before `Load` |
| **Destroyed objects** (`Destroy`) | Their state stays in the save, but nothing restores them. For things that disappear, use `SetActive(false)` + `SaveActiveState` instead of `Destroy` |

### Rules that keep old saves working

Your save files live longer than your code. These changes break the link between a save and an object:

- **Don't remove and re-add `SaveableEntity`**: the object gets a new id and its old saved state is
  lost. Use **New Id** only on purpose.
- **Don't rename a saveable component's class** without keeping its key: the state is stored under the
  class name. After a rename, add `public override string SaveKey => "OldClassName";`.
- **Renaming a field in `State`** loses that value in old saves. Adding fields is safe (old saves get
  the default value); removing fields is safe (the old value is ignored). For renames, use a migration.
- **Never put Unity object references in `State`** (`GameObject`, `Transform`, `Material`, `Sprite`…).
  They can't be written to a file. Save a name, an id or an index and look the object up in `Restore`.

### Other things to watch out for

- **Call `Save`/`Load` at a calm moment**, not in the middle of a physics step: e.g. from a menu,
  a checkpoint, or `AutoSaver`.
- **`OnApplicationQuit` is not reliable on mobile**: the OS may kill the app. `AutoSaver` also saves
  when the app goes to the background (`saveOnPause`).
- **Several scenes:** objects that aren't in the loaded scene keep their saved state; saving in
  scene B doesn't forget the objects of scene A.
- **`SaveSystem.BeforeSave`** is the right moment to copy values into `SaveSystem.Current`;
  **`SaveSystem.AfterLoad`** to read them back.
- **`SaveSystem.NewGame()`** clears the data in memory (not the files on disk).
- **WebGL and consoles** are not tested yet.

### Troubleshooting

| Problem | Likely cause |
|---|---|
| An object doesn't go back after Load | It has no `SaveTransform`, or a script of yours moves it back (see above) |
| Console: *"… id'si yok"* (has no id) | Object created at runtime without `SetId` |
| Console: *"Aynı id'ye sahip iki obje"* (two objects with the same id) | Object cloned by code; give it a unique id |
| A value comes back as the default | Key name typo, a renamed field, or a changed type |
| Console: *"… kaydı okunamadı"* (save can't be read) | File broken and no backup, or the encryption key changed |

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
| `Tests/Runtime/SaveSystemTests.cs` | 16 tests |

## Tests

16 PlayMode tests: all data types round-trip, defaults for missing values, invalid slot names, scene
objects (transform, active state, custom component, velocity, inactive objects, CharacterController), objects from other
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

## Kendi modellerin ve prefab'ların

Asset Store'dan ya da kendi Blender modellerinden gelen karakterler, eşyalar ve düşmanlar da aynı
şekilde kaydedilir. Doğru geri yüklenip yüklenmeyeceklerini aşağıdaki kurallar belirler.

### Kurulum listesi

| # | Yap | Neden |
|---|---|---|
| 1 | `SaveableEntity`'yi modelin **kök** objesine koy (hareket ettirdiğin obje), alt mesh'e değil | `SaveTransform` üzerinde durduğu objenin konumunu kaydeder |
| 2 | Her kopyası kaydedilecekse **prefab'a** ekle | Prefab asset'inin kendisi id almaz; sahneye konan her kopya otomatik kendi id'sini alır |
| 3 | Mümkünse objeleri sahneye **editörde** koy | Sabit bir id alırlar. Oyun sırasında kodla yaratılanlar `SetId` ister (aşağıda) |
| 4 | Kaydedilecek her şey için bir bileşen ekle: `SaveTransform` (nerede), `SaveActiveState` (var mı), kendi bileşenin (can, envanter, açık/kapalı) | Sadece `ISaveable` olan bileşenler kaydedilir; hiçbir şey "kendiliğinden" kaydedilmez |

### Hazır çalışanlar

| Modelde | Sonuç |
|---|---|
| `Rigidbody` | Konum, rotasyon ve hız geri gelir |
| `CharacterController` (çoğu karakter kontrolcüsü) | Halledildi: karakter geri taşınırken kısa süre kapatılıp tekrar açılır |
| `NavMeshAgent` (yapay zeka düşmanları) | Halledildi: agent `Warp` ile taşınır, eski yerine geri zıplamaz |
| Pasif obje (toplanmış eşya) | Pasif olsa da bulunur ve geri yüklenir |
| Kendi `SaveableEntity`'si olmayan alt objeler | Ebeveynleriyle birlikte hareket eder; kendi durumları kaydedilmez |

### Dikkat isteyenler

| Durum | Ne yapmalı |
|---|---|
| **Animator** (kapının açılma animasyonu, karakterin pozu) | Kaydedilmez. Onu yöneten *durumu* (ör. `isOpen`) kendi bileşeninde kaydet ve `Restore`'da Animator parametresini ayarla |
| Tek modelin altında **ragdoll / birden çok Rigidbody** | Sadece `SaveTransform` olan obje geri yüklenir. Pozunu koruması gereken her parçaya `SaveableEntity` + `SaveTransform` ekle ya da karakteri ayakta kaydet |
| **Objeyi her kare hareket ettiren bir script** (kendi oyuncu kontrolcün) | Kendi konum/hedef değişkenlerini tutuyorsa onları da kendi bileşeninde geri yükle, yoksa objeyi eski yerine taşır |
| **Kodla yaratılan objeler** (`Instantiate`) | Prefab'ın boş id'sini ya da kopyalanan objenin id'sini alırlar. Her birine sabit bir id ver: `SetId("enemy_" + index)`, ve `Load`'dan önce onları tekrar yarat |
| **Yok edilen objeler** (`Destroy`) | Durumları kayıtta kalır ama onları geri getiren bir şey yok. Kaybolan şeyler için `Destroy` yerine `SetActive(false)` + `SaveActiveState` kullan |

### Eski kayıtları bozmamak için kurallar

Kayıt dosyaların kodundan uzun yaşar. Şu değişiklikler kayıt ile obje arasındaki bağı koparır:

- **`SaveableEntity`'yi silip tekrar ekleme**: obje yeni id alır, eski kayıtlı durumu kaybolur.
  **Yeni Id** düğmesini sadece bilerek kullan.
- **Kaydedilebilir bileşenin sınıf adını** anahtarını korumadan **değiştirme**: durum sınıf adıyla
  saklanır. Ad değiştirdikten sonra `public override string SaveKey => "EskiSinifAdi";` ekle.
- **`State` içindeki bir alanın adını değiştirmek** eski kayıtlarda o değeri kaybettirir. Alan eklemek
  güvenli (eski kayıtlar varsayılan değeri alır); alan silmek güvenli (eski değer yok sayılır). Ad
  değişikliği için sürüm geçişi (migration) kullan.
- **`State`'e asla Unity obje referansı koyma** (`GameObject`, `Transform`, `Material`, `Sprite`…).
  Dosyaya yazılamazlar. Bir isim, id ya da sıra numarası kaydet ve `Restore`'da objeyi bul.

### Dikkat edilecek diğer şeyler

- **`Save`/`Load`'u sakin bir anda çağır**, fizik adımının ortasında değil: ör. menüden, bir kontrol
  noktasında ya da `AutoSaver` ile.
- **`OnApplicationQuit` mobilde güvenilir değil**: işletim sistemi uygulamayı öldürebilir. `AutoSaver`
  uygulama arka plana atılınca da kaydeder (`saveOnPause`).
- **Birden çok sahne:** yüklü sahnede olmayan objeler kayıtlı durumlarını korur; B sahnesinde
  kaydetmek A sahnesinin objelerini unutmaz.
- **`SaveSystem.BeforeSave`** değerleri `SaveSystem.Current`'a yazmak için doğru andır;
  **`SaveSystem.AfterLoad`** geri okumak için.
- **`SaveSystem.NewGame()`** bellekteki veriyi temizler (diskteki dosyaları değil).
- **WebGL ve konsollar** henüz test edilmedi.

### Sorun giderme

| Sorun | Muhtemel sebep |
|---|---|
| Obje Load'dan sonra eski yerine dönmüyor | `SaveTransform` yok ya da senin bir script'in onu geri taşıyor (yukarıya bak) |
| Console: *"… id'si yok"* | Oyun sırasında `SetId` verilmeden yaratılmış obje |
| Console: *"Aynı id'ye sahip iki obje"* | Kodla kopyalanmış obje; benzersiz bir id ver |
| Bir değer varsayılan olarak geri geliyor | Anahtar adında yazım hatası, adı değişmiş alan ya da değişmiş tip |
| Console: *"… kaydı okunamadı"* | Dosya bozuk ve yedek yok, ya da şifreleme anahtarı değişmiş |

Dosya listesi ve testler (16 test) için İngilizce bölümdeki [Files](#files) ve [Tests](#tests) kısımlarına bak.

## Lisans

[MIT](LICENSE.md)

---

🤖 *This project was developed with the help of AI tools.*  
🤖 *Bu proje yapay zekâ araçlarının yardımıyla geliştirilmiştir.*
