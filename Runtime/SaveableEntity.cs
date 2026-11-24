using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Atthree.Saving
{
    /// <summary>
    /// Sahnedeki bir objeyi kayıt sistemine tanıtır. Objenin benzersiz bir kimliği (id) olur; kayıtta
    /// "şu id'li objenin durumu şu" diye saklanır. Objedeki tüm <see cref="ISaveable"/> bileşenleri
    /// (SaveTransform, kendi scriptlerin...) bu id altında kaydedilir.
    ///
    /// Id editörde otomatik verilir. Objeyi kopyalarsan (Ctrl+D) kopyaya yeni id verilir.
    /// Oyun sırasında yaratılan objeler için <see cref="SetId"/> ile kendin ver (ör. "enemy_" + index).
    /// </summary>
    [DisallowMultipleComponent]
    public class SaveableEntity : MonoBehaviour
    {
        [SerializeField] string id = "";

        public string Id => id;

        public void SetId(string value) => id = value;

        /// <summary>Objedeki tüm kaydedilebilir bileşenlerin durumu.</summary>
        public Dictionary<string, JToken> CaptureState()
        {
            var state = new Dictionary<string, JToken>();
            foreach (var saveable in GetComponents<ISaveable>())
            {
                try { state[saveable.SaveKey] = saveable.CaptureState(); }
                catch (Exception e) { Debug.LogError($"[SaveSystem] {name}/{saveable.SaveKey} kaydedilemedi: {e.Message}", this); }
            }
            return state;
        }

        public void RestoreState(Dictionary<string, JToken> state)
        {
            foreach (var saveable in GetComponents<ISaveable>())
            {
                if (!state.TryGetValue(saveable.SaveKey, out var token)) continue;
                try { saveable.RestoreState(token); }
                catch (Exception e) { Debug.LogError($"[SaveSystem] {name}/{saveable.SaveKey} yüklenemedi: {e.Message}", this); }
            }
        }

#if UNITY_EDITOR
        // Editörde: sahnedeki objeye id yoksa ya da başka bir objeyle aynıysa (kopyalandıysa) yeni id ver.
        // Prefab asset'inin kendisine id verilmez; sahnedeki her kopya kendi id'sini alır.
        void OnValidate()
        {
            if (Application.isPlaying) return;
            if (!gameObject.scene.IsValid()) return; // prefab asset / prefab modu dışında
            if (string.IsNullOrEmpty(id) || IsDuplicate())
            {
                id = Guid.NewGuid().ToString("N");
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        bool IsDuplicate()
        {
            foreach (var other in FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (other != this && other.id == id) return true;
            return false;
        }

        /// <summary>Editör aracı: yeni id ver.</summary>
        public void RegenerateId()
        {
            id = Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
