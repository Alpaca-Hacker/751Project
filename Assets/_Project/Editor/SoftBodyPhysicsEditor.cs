#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace SoftBody.Scripts.Editor
{
    [CustomEditor(typeof(SoftBodyPhysics))]
    public class SoftBodyPhysicsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            var softBody = (SoftBodyPhysics)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Physics Data Tools", EditorStyles.boldLabel);
            
            if (softBody.HasPreGeneratedData)
            {
                var data = softBody.GetPreGeneratedData();
                EditorGUILayout.HelpBox($"Pre-generated data available:\n" +
                    $"• {data.particleCount} particles\n" +
                    $"• {data.constraintCount} constraints\n" +
                    $"• {data.volumeConstraintCount} volume constraints\n" +
                    $"• Mesh: {data.meshName}", MessageType.Info);
                
                if (GUILayout.Button("Clear Physics Data"))
                {
                    softBody.ClearPhysicsData();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No pre-generated physics data. This will use slow runtime generation.", 
                    MessageType.Warning);
                
                if (GUILayout.Button("Generate Physics Data"))
                {
                    softBody.GeneratePhysicsDataInEditor();
                }
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate for This Object"))
            {
                softBody.GeneratePhysicsDataInEditor();
            }
            
            if (GUILayout.Button("Generate for All in Scene"))
            {
                GenerateForAllInScene();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void GenerateForAllInScene()
        {
            var allSoftBodies = FindObjectsByType<SoftBodyPhysics>(FindObjectsSortMode.None);
            int generated = 0;
            
            foreach (var softBody in allSoftBodies)
            {
                try
                {
                    softBody.GeneratePhysicsDataInEditor();
                    generated++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to generate for {softBody.name}: {e.Message}");
                }
            }
            
            Debug.Log($"Generated physics data for {generated}/{allSoftBodies.Length} soft bodies in scene");
        }
    }
}
#endif