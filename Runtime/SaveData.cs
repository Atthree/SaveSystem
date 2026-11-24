using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Atthree.Saving
{
    /// <summary>
    /// Anahtar-değer kayıt kutusu. Her tipi saklayabilir: int, string, Vector3, List, kendi sınıfların...
    ///
    ///   data.Set("coins", 120);
    ///   data.Set("inventory", new List&lt;string&gt; { "sword", "potion" });
    ///   int coins = data.Get("coins", 0);          // yoksa 0
    ///
    /// İçeride değerler JSON ağacı (JToken) olarak tutulur; dosyaya yazmak bu yüzden kolaydır.
    /// </summary>
    public class SaveData
    {
        readonly Dictionary<string, JToken> values;

        public SaveData() : this(new Dictionary<string, JToken>()) { }

        internal SaveData(Dictionary<string, JToken> values) => this.values = values;

        internal Dictionary<string, JToken> Values => values;

        public IEnumerable<string> Keys => values.Keys;
        public int Count => values.Count;

        public bool Has(string key) => values.ContainsKey(key);

        public void Set<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("Anahtar boş olamaz.", nameof(key));
            values[key] = value == null ? JValue.CreateNull() : JToken.FromObject(value, SaveSerializer.Instance);
        }

        /// <summary>Değeri okur; anahtar yoksa ya da tip uymuyorsa <paramref name="defaultValue"/> döner.</summary>
        public T Get<T>(string key, T defaultValue = default)
        {
            return TryGet(key, out T value) ? value : defaultValue;
        }

        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            if (!values.TryGetValue(key, out var token) || token == null || token.Type == JTokenType.Null) return false;
            try
            {
                value = token.ToObject<T>(SaveSerializer.Instance);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] '{key}' okunamadı ({typeof(T).Name}): {e.Message}");
                return false;
            }
        }

        public bool Remove(string key) => values.Remove(key);
        public void Clear() => values.Clear();

        /// <summary>Değeri ham JSON olarak verir (hata ayıklama için).</summary>
        public string ToJson(bool pretty = true) =>
            JsonConvert.SerializeObject(values, pretty ? Formatting.Indented : Formatting.None);
    }

    /// <summary>Paketin her yerde kullandığı tek JsonSerializer (Unity dönüştürücüleriyle).</summary>
    public static class SaveSerializer
    {
        static JsonSerializer instance;

        public static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            Converters = UnityJsonConverters.All,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None, // güvenlik: dosyadan tip adı okuyup obje yaratma
        };

        public static JsonSerializer Instance => instance ??= JsonSerializer.Create(Settings);
    }
}
