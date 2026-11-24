using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Atthree.Saving
{
    /// <summary>
    /// Bir kayıt slotunun özeti: "Kayıt 2 — Orman, 3 sa 12 dk, 24 Kasım 18:40".
    /// Ayrı küçük bir dosyada da tutulur; böylece kayıt menüsü büyük dosyaları açmadan listelenir.
    /// </summary>
    [Serializable]
    public class SaveMetadata
    {
        public string slot;
        public string description;
        public string scene;
        /// <summary>UTC zaman, ISO 8601.</summary>
        public string savedAtUtc;
        /// <summary>Toplam oynama süresi (saniye).</summary>
        public double playTimeSeconds;
        /// <summary>Kaydın formatı (SaveSettings.version). Eski kayıtları dönüştürmek için.</summary>
        public int version;
        public string gameVersion;

        [JsonIgnore]
        public DateTime SavedAtLocal =>
            DateTime.TryParse(savedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)
                ? t.ToLocalTime() : DateTime.MinValue;

        [JsonIgnore]
        public TimeSpan PlayTime => TimeSpan.FromSeconds(playTimeSeconds);

        public override string ToString() =>
            $"{slot}: {description} ({scene}, {(int)PlayTime.TotalHours}:{PlayTime.Minutes:00}, {SavedAtLocal:g})";
    }

    /// <summary>Diskteki dosyanın tam içeriği.</summary>
    internal class SaveFile
    {
        public SaveMetadata meta = new SaveMetadata();
        /// <summary>SaveSystem.Current anahtar-değerleri.</summary>
        public Dictionary<string, JToken> data = new Dictionary<string, JToken>();
        /// <summary>Sahne objeleri: SaveableEntity id → (bileşen anahtarı → durum).</summary>
        public Dictionary<string, Dictionary<string, JToken>> entities = new Dictionary<string, Dictionary<string, JToken>>();
    }
}
