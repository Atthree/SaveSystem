using UnityEngine;

namespace Atthree.Saving.Demo
{
    /// <summary>
    /// Demo: kendi kaydedilebilir bileşenini nasıl yazacağının örneği. Tıklayınca renk değiştirir;
    /// renk ve tıklama sayısı kaydedilir.
    /// </summary>
    [AddComponentMenu("Save System/Demo/Demo Lamp")]
    public class DemoLamp : SaveableBehaviour<DemoLamp.State>, IDemoClickable
    {
        // Kaydedilecek her şey burada. Public alanlar JSON'a yazılır.
        public struct State
        {
            public Color color;
            public int clicks;
        }

        int clicks;
        Renderer cachedRenderer;

        Renderer Rend => cachedRenderer != null ? cachedRenderer : cachedRenderer = GetComponent<Renderer>();

        public void OnDemoClick()
        {
            clicks++;
            SetColor(Color.HSVToRGB(Random.value, 0.8f, 1f));
        }

        void SetColor(Color color)
        {
            var material = Rend.material; // örnek başına kopya
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        }

        protected override State Capture() => new State { color = Rend.material.color, clicks = clicks };

        protected override void Restore(State state)
        {
            clicks = state.clicks;
            SetColor(state.color);
        }
    }
}
