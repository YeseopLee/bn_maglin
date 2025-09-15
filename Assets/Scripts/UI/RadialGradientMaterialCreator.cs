using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UI
{
    /// <summary>
    /// Radial Gradient 머티리얼을 쉽게 생성하고 관리하는 유틸리티 클래스
    /// </summary>
    public class RadialGradientMaterialCreator : MonoBehaviour
    {
        [Header("Radial Gradient Settings")]
        [SerializeField] private Color centerColor = Color.white;
        [SerializeField] private Color edgeColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField][Range(0.1f, 5.0f)] private float gradientPower = 1.0f;
        [SerializeField][Range(0.0f, 1.0f)] private float gradientOffset = 0.0f;
        [SerializeField][Range(0.1f, 2.0f)] private float gradientScale = 0.7f;

        [Header("Material Reference")]
        [SerializeField] private Material radialGradientMaterial;

        private void Start()
        {
            // 런타임에서 머티리얼 설정 적용
            if (radialGradientMaterial != null)
            {
                ApplySettingsToMaterial();
            }
        }

        /// <summary>
        /// 현재 설정을 머티리얼에 적용
        /// </summary>
        public void ApplySettingsToMaterial()
        {
            if (radialGradientMaterial == null) return;

            radialGradientMaterial.SetColor("_CenterColor", centerColor);
            radialGradientMaterial.SetColor("_EdgeColor", edgeColor);
            radialGradientMaterial.SetFloat("_GradientPower", gradientPower);
            radialGradientMaterial.SetFloat("_GradientOffset", gradientOffset);
            radialGradientMaterial.SetFloat("_GradientScale", gradientScale);
        }

        /// <summary>
        /// 새로운 Radial Gradient 머티리얼 생성
        /// </summary>
        public Material CreateNewMaterial()
        {
            Shader radialGradientShader = Shader.Find("UI/RadialGradient");
            if (radialGradientShader == null)
            {
                Debug.LogError("UI/RadialGradient 셰이더를 찾을 수 없습니다!");
                return null;
            }

            Material newMaterial = new Material(radialGradientShader);
            newMaterial.name = "RadialGradient_Material";

            // 기본 설정 적용
            newMaterial.SetColor("_CenterColor", centerColor);
            newMaterial.SetColor("_EdgeColor", edgeColor);
            newMaterial.SetFloat("_GradientPower", gradientPower);
            newMaterial.SetFloat("_GradientOffset", gradientOffset);
            newMaterial.SetFloat("_GradientScale", gradientScale);

            return newMaterial;
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(RadialGradientMaterialCreator))]
        public class RadialGradientMaterialCreatorEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                RadialGradientMaterialCreator creator = (RadialGradientMaterialCreator)target;
                
                DrawDefaultInspector();
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button("머티리얼에 설정 적용"))
                {
                    creator.ApplySettingsToMaterial();
                    EditorUtility.SetDirty(creator.radialGradientMaterial);
                }
                
                if (GUILayout.Button("새 머티리얼 생성"))
                {
                    Material newMaterial = creator.CreateNewMaterial();
                    if (newMaterial != null)
                    {
                        string path = "Assets/Materials/RadialGradient_Material.mat";
                        
                        // Materials 폴더가 없으면 생성
                        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                        {
                            AssetDatabase.CreateFolder("Assets", "Materials");
                        }
                        
                        AssetDatabase.CreateAsset(newMaterial, path);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        
                        // 생성된 머티리얼을 선택
                        Selection.activeObject = newMaterial;
                        EditorGUIUtility.PingObject(newMaterial);
                        
                        Debug.Log($"새 Radial Gradient 머티리얼이 생성되었습니다: {path}");
                    }
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "1. '새 머티리얼 생성' 버튼을 클릭하여 머티리얼을 만드세요.\n" +
                    "2. 설정을 조정하고 '머티리얼에 설정 적용' 버튼을 클릭하세요.\n" +
                    "3. UI Image 컴포넌트의 Material 필드에 생성된 머티리얼을 할당하세요.",
                    MessageType.Info
                );
            }
        }
#endif
    }
}
