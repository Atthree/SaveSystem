using UnityEditor;
using UnityEngine;

namespace Atthree.Saving.Editor
{
    [CustomEditor(typeof(SaveableEntity))]
    public class SaveableEntityEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var entity = (SaveableEntity)target;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Id", entity.Id);

            if (!entity.gameObject.scene.IsValid())
                EditorGUILayout.HelpBox("Prefab asset'inde id yok; sahneye konan her kopya kendi id'sini alır.", MessageType.Info);
            else if (string.IsNullOrEmpty(entity.Id))
                EditorGUILayout.HelpBox("Id yok. Oyun sırasında yaratılıyorsa SetId(...) ile ver.", MessageType.Warning);

            if (GUILayout.Button("Yeni Id"))
            {
                Undo.RecordObject(entity, "New Save Id");
                entity.RegenerateId();
            }

            var saveables = entity.GetComponents<ISaveable>();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Kaydedilen bileşenler", EditorStyles.boldLabel);
            if (saveables.Length == 0)
                EditorGUILayout.HelpBox("Bu objede kaydedilecek bileşen yok. Save Transform, Save Active State " +
                                        "ya da kendi SaveableBehaviour'unu ekle.", MessageType.Warning);
            foreach (var s in saveables)
                EditorGUILayout.LabelField("• " + s.SaveKey);
        }
    }
}
