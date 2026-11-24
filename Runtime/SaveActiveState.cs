using UnityEngine;

namespace Atthree.Saving
{
    /// <summary>
    /// Objenin açık/kapalı olmasını kaydeder. Toplanan altınlar, açılmış kapılar, ölmüş düşmanlar...
    /// (Kapalı objeler de bulunur; kayıt sistemi pasif objeleri de arar.)
    /// </summary>
    [AddComponentMenu("Save System/Save Active State")]
    public class SaveActiveState : SaveableBehaviour<bool>
    {
        protected override bool Capture() => gameObject.activeSelf;
        protected override void Restore(bool active) => gameObject.SetActive(active);
    }
}
