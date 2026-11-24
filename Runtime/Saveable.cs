using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Atthree.Saving
{
    /// <summary>
    /// Durumu kaydedilebilen bileşen. Genelde bunu doğrudan değil, <see cref="SaveableBehaviour{TState}"/>
    /// sınıfını kullan. Aynı objede bir <see cref="SaveableEntity"/> olmalı.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Bu bileşenin kayıttaki adı (aynı objede benzersiz olmalı).</summary>
        string SaveKey { get; }
        JToken CaptureState();
        void RestoreState(JToken state);
    }

    /// <summary>
    /// Kendi bileşenini kaydedilebilir yapmanın en kolay yolu:
    ///
    ///   public class Chest : SaveableBehaviour&lt;Chest.State&gt;
    ///   {
    ///       public struct State { public bool opened; public int gold; }
    ///       bool opened; int gold;
    ///       protected override State Capture() => new State { opened = opened, gold = gold };
    ///       protected override void Restore(State s) { opened = s.opened; gold = s.gold; }
    ///   }
    ///
    /// State içindeki public alanlar JSON'a yazılır (int, string, Vector3, List, iç içe sınıflar...).
    /// </summary>
    [RequireComponent(typeof(SaveableEntity))]
    public abstract class SaveableBehaviour<TState> : MonoBehaviour, ISaveable
    {
        public virtual string SaveKey => GetType().Name;

        /// <summary>Kaydedilecek durumu oluştur.</summary>
        protected abstract TState Capture();

        /// <summary>Yüklenen durumu bileşene uygula.</summary>
        protected abstract void Restore(TState state);

        JToken ISaveable.CaptureState() => JToken.FromObject(Capture(), SaveSerializer.Instance);

        void ISaveable.RestoreState(JToken state) => Restore(state.ToObject<TState>(SaveSerializer.Instance));
    }
}
