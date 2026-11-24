using System.IO;
using Atthree.Saving.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Atthree.Saving.Editor
{
    public static class SaveMenus
    {
        [MenuItem("Tools/Save System/Open Save Folder", priority = 1)]
        public static void OpenSaveFolder()
        {
            string path = SaveSystem.Settings.Storage.Location;
            if (path == null) return;
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Tools/Save System/Delete All Saves", priority = 2)]
        static void DeleteAll()
        {
            var storage = SaveSystem.Settings.Storage;
            if (!EditorUtility.DisplayDialog("Tüm kayıtları sil",
                    $"Bu klasördeki TÜM kayıtlar ve ayarlar silinecek:\n{storage.Location}", "Sil", "Vazgeç"))
                return;
            foreach (var name in storage.List()) storage.Delete(name);
            Debug.Log("[SaveSystem] Tüm kayıtlar silindi.");
        }

        [MenuItem("Tools/Save System/Create Demo Scene", priority = 20)]
        static void CreateDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 7f, -9f), Quaternion.Euler(35f, 0f, 0f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);

            var panel = new GameObject("Save Demo");
            panel.AddComponent<SaveDemo>();

            // Kutular: konumları kaydedilir (SaveTransform).
            for (int i = 0; i < 6; i++)
            {
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = "Crate " + i;
                crate.transform.position = new Vector3(-3f + (i % 3) * 1.5f, 0.5f + (i / 3) * 1.1f, 2f);
                crate.AddComponent<Rigidbody>();
                crate.AddComponent<SaveableEntity>();
                crate.AddComponent<SaveTransform>();
            }

            // Altınlar: toplandı mı (SaveActiveState) + sayaç (SaveSystem.Current).
            for (int i = 0; i < 4; i++)
            {
                var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                coin.name = "Coin " + i;
                coin.transform.position = new Vector3(2f + i * 1.2f, 0.6f, -1f);
                coin.transform.localScale = new Vector3(0.6f, 0.05f, 0.6f);
                coin.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Tint(coin, new Color(1f, 0.8f, 0.1f));
                coin.AddComponent<SaveableEntity>();
                coin.AddComponent<SaveActiveState>();
                coin.AddComponent<DemoCoin>();
            }

            // Lamba: kendi SaveableBehaviour'umuz (renk + tıklama sayısı).
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = "Lamp";
            lamp.transform.position = new Vector3(-1f, 1f, -2f);
            lamp.AddComponent<SaveableEntity>();
            lamp.AddComponent<DemoLamp>();

            // Otomatik kayıt örneği.
            var auto = new GameObject("Auto Saver");
            var saver = auto.AddComponent<AutoSaver>();
            saver.intervalSeconds = 60f;

            // Id'ler OnValidate'te verilir; yeni eklenen bileşenler için tetikle.
            foreach (var entity in Object.FindObjectsByType<SaveableEntity>(FindObjectsSortMode.None))
                if (string.IsNullOrEmpty(entity.Id)) entity.RegenerateId();

            Selection.activeGameObject = panel;
            Debug.Log("[SaveSystem] Demo sahnesi kuruldu. Play'e bas; kutuları Boşluk ile it, altınlara/lambaya tıkla, F5 kaydet, F9 yükle.");
        }

        static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            renderer.sharedMaterial = material;
        }
    }
}
