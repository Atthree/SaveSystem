using UnityEngine;

namespace Atthree.Saving
{
    /// <summary>
    /// Otomatik kayıt: belirli aralıklarla, oyundan çıkarken ve (mobilde) uygulama arka plana atılınca kaydeder.
    /// Sahnede bir objeye ekle, slot adını ver.
    /// </summary>
    [AddComponentMenu("Save System/Auto Saver")]
    public class AutoSaver : MonoBehaviour
    {
        [Tooltip("Otomatik kaydın yazılacağı slot.")]
        public string slot = "autosave";
        [Tooltip("Kaç saniyede bir kaydedilsin (0 = sadece çıkışta).")]
        public float intervalSeconds = 120f;
        public bool saveOnQuit = true;
        [Tooltip("Mobilde uygulama arka plana atılınca kaydet (oyuncu oyunu kapatmadan öldürebilir).")]
        public bool saveOnPause = true;
        [Tooltip("Oyun açılınca bu slot varsa yükle.")]
        public bool loadOnStart;

        float timer;

        void Start()
        {
            if (loadOnStart && SaveSystem.Exists(slot)) SaveSystem.Load(slot);
        }

        void Update()
        {
            if (intervalSeconds <= 0f) return;
            timer += Time.unscaledDeltaTime;
            if (timer >= intervalSeconds)
            {
                timer = 0f;
                SaveNow();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && saveOnPause) SaveNow();
        }

        void OnApplicationQuit()
        {
            if (saveOnQuit) SaveNow();
        }

        public void SaveNow()
        {
            SaveSystem.Save(slot, "Otomatik kayıt");
            SaveSystem.SavePreferences();
        }
    }
}
