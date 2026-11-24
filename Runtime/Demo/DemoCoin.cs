using UnityEngine;

namespace Atthree.Saving.Demo
{
    /// <summary>
    /// Demo: tıklanınca toplanan altın. Hem <see cref="SaveActiveState"/> (toplandı mı) hem de
    /// SaveSystem.Current'taki "coins" sayacı (anahtar-değer) örneği.
    /// </summary>
    [AddComponentMenu("Save System/Demo/Demo Coin")]
    public class DemoCoin : MonoBehaviour, IDemoClickable
    {
        void Update()
        {
            transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
        }

        public void OnDemoClick()
        {
            SaveSystem.Current.Set("coins", SaveSystem.Current.Get("coins", 0) + 1);
            gameObject.SetActive(false);
        }
    }
}
