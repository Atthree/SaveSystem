using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Atthree.Saving.Editor
{
    /// <summary>Tools > Save System > Save Browser: kayıtları listele, içeriğini gör, sil.</summary>
    public class SaveBrowserWindow : EditorWindow
    {
        List<SaveMetadata> slots = new List<SaveMetadata>();
        string selected;
        string json;
        Vector2 listScroll, jsonScroll;

        [MenuItem("Tools/Save System/Save Browser", priority = 0)]
        static void Open() => GetWindow<SaveBrowserWindow>("Save Browser").Refresh();

        void OnFocus() => Refresh();

        void Refresh()
        {
            slots = SaveSystem.ListSlots();
            if (selected != null) json = SaveSystem.ReadRawJson(selected);
            Repaint();
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Yenile", EditorStyles.toolbarButton, GUILayout.Width(60))) Refresh();
                if (GUILayout.Button("Klasörü Aç", EditorStyles.toolbarButton, GUILayout.Width(80))) SaveMenus.OpenSaveFolder();
                GUILayout.FlexibleSpace();
                GUILayout.Label(SaveSystem.Settings.Storage.Location ?? "(bellek)", EditorStyles.miniLabel);
            }

            if (slots.Count == 0)
            {
                EditorGUILayout.HelpBox("Hiç kayıt yok. Play modunda kaydet, sonra Yenile.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                // Sol: slot listesi
                using (var scroll = new EditorGUILayout.ScrollViewScope(listScroll, GUILayout.Width(280)))
                {
                    listScroll = scroll.scrollPosition;
                    foreach (var meta in slots)
                    {
                        var style = meta.slot == selected ? EditorStyles.helpBox : EditorStyles.label;
                        using (new EditorGUILayout.VerticalScope(style))
                        {
                            if (GUILayout.Button($"{meta.slot}  ·  {meta.description}", EditorStyles.boldLabel))
                            {
                                selected = meta.slot;
                                json = SaveSystem.ReadRawJson(meta.slot);
                            }
                            EditorGUILayout.LabelField($"{meta.SavedAtLocal:g}  ·  {meta.scene}  ·  oynama {meta.PlayTime:hh\\:mm\\:ss}  ·  v{meta.version}", EditorStyles.miniLabel);
                        }
                    }
                }

                // Sağ: seçili kaydın JSON'u
                using (new EditorGUILayout.VerticalScope())
                {
                    if (selected == null)
                    {
                        EditorGUILayout.HelpBox("İçeriğini görmek için soldan bir kayıt seç.", MessageType.None);
                        return;
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(selected, EditorStyles.boldLabel);
                        if (GUILayout.Button("Sil", GUILayout.Width(50)) &&
                            EditorUtility.DisplayDialog("Kaydı sil", $"'{selected}' kaydı silinsin mi?", "Sil", "Vazgeç"))
                        {
                            SaveSystem.Delete(selected);
                            selected = null;
                            json = null;
                            Refresh();
                            GUIUtility.ExitGUI();
                        }
                    }
                    using (var scroll = new EditorGUILayout.ScrollViewScope(jsonScroll))
                    {
                        jsonScroll = scroll.scrollPosition;
                        EditorGUILayout.TextArea(json ?? "", EditorStyles.textArea, GUILayout.ExpandHeight(true));
                    }
                }
            }
        }
    }
}
