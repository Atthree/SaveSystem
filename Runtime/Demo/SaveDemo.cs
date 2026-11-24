using UnityEngine;

namespace Atthree.Saving.Demo
{
    /// <summary>Demo objeleri: tıklanınca ne yapacakları.</summary>
    public interface IDemoClickable { void OnDemoClick(); }

    /// <summary>
    /// Kayıt sistemini denemek için ekran paneli: 3 slot, kaydet / yükle / sil, F5 hızlı kayıt, F9 hızlı yükleme.
    /// Tools > Save System > Create Demo Scene ile kurulur. Kendi oyununda kullanmana gerek yok.
    /// </summary>
    [AddComponentMenu("Save System/Demo/Save Demo Panel")]
    public class SaveDemo : MonoBehaviour
    {
        static readonly string[] Slots = { "slot1", "slot2", "slot3" };
        string message = "";
        GUIStyle label, title;
        Rect panelRect;

        void Start()
        {
            // Preferences örneği: oyunun kaç kez açıldığı, kayıt slotlarından bağımsız saklanır.
            int launches = SaveSystem.Preferences.Get("launchCount", 0) + 1;
            SaveSystem.Preferences.Set("launchCount", launches);
            SaveSystem.SavePreferences();
            message = $"Oyun {launches}. kez açıldı (Preferences örneği). F5 kaydet, F9 yükle.";
        }

        void OnGUI()
        {
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            title ??= new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold };

            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F5) { Save("slot1"); e.Use(); }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F9) { Load("slot1"); e.Use(); }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space) { PushCrates(); e.Use(); }
            // Tıklama: OnMouseDown eski Input Manager ister; IMGUI olayları her iki girdi sisteminde de çalışır.
            if (e.type == EventType.MouseDown && e.button == 0 && !panelRect.Contains(e.mousePosition)) { ClickWorld(e.mousePosition); }

            panelRect = new Rect(12, 12, 460, Screen.height - 24);
            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("Save System Demo", title);
            GUILayout.Label($"Oynama süresi: {System.TimeSpan.FromSeconds(SaveSystem.PlayTimeSeconds):hh\\:mm\\:ss}   " +
                            $"Toplanan altın: {SaveSystem.Current.Get("coins", 0)}", label);
            GUILayout.Space(6);

            foreach (var slot in Slots)
            {
                GUILayout.BeginHorizontal();
                bool exists = SaveSystem.Exists(slot);
                GUILayout.Label(slot, label, GUILayout.Width(60));
                if (GUILayout.Button("Kaydet", GUILayout.Width(70))) Save(slot);
                GUI.enabled = exists;
                if (GUILayout.Button("Yükle", GUILayout.Width(60))) Load(slot);
                if (GUILayout.Button("Sil", GUILayout.Width(40))) { SaveSystem.Delete(slot); message = $"{slot} silindi."; }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("Kayıtlar (en yeni önce):", label);
            foreach (var meta in SaveSystem.ListSlots())
                GUILayout.Label($"• {meta.slot}: {meta.description}  —  {meta.SavedAtLocal:HH:mm:ss}, oynama {meta.PlayTime:hh\\:mm\\:ss}", label);

            GUILayout.Space(6);
            if (GUILayout.Button("Yeni Oyun (bellekteki veriyi temizle)")) { SaveSystem.NewGame(); message = "Yeni oyun: veriler sıfırlandı (diskteki kayıtlar duruyor)."; }
            GUILayout.Label(message, label);
            GUILayout.EndArea();
        }

        void Save(string slot)
        {
            SaveSystem.Save(slot, $"Altın: {SaveSystem.Current.Get("coins", 0)}");
            message = $"{slot} kaydedildi. Konum: {SaveSystem.Settings.Storage.Location}";
        }

        void Load(string slot)
        {
            message = SaveSystem.Load(slot) ? $"{slot} yüklendi." : $"{slot} yüklenemedi.";
        }

        void ClickWorld(Vector2 guiPosition)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var ray = cam.ScreenPointToRay(new Vector3(guiPosition.x, Screen.height - guiPosition.y, 0f));
            if (Physics.Raycast(ray, out var hit, 200f) && hit.collider.TryGetComponent(out IDemoClickable clickable))
                clickable.OnDemoClick();
        }

        void PushCrates()
        {
            foreach (var body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                body.AddForce(new Vector3(Random.Range(-1f, 1f), 1f, Random.Range(-1f, 1f)) * 6f, ForceMode.VelocityChange);
        }
    }
}
