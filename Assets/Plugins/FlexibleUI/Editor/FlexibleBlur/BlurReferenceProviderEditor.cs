using UnityEditor;
using UnityEngine;

namespace JeffGrawAssets.FlexibleUI
{
[CustomEditor(typeof(BlurReferenceProvider))]
public class BlurReferenceProviderEditor : Editor
{
    private GameObject go;
    private SerializedProperty cameraProperty;
    private SerializedProperty featureNumberProperty;
    
    void OnEnable()
    {
        go = ((BlurReferenceProvider)serializedObject.targetObject).gameObject;
        cameraProperty = serializedObject.FindProperty(nameof(BlurReferenceProvider.cameraReference));
        featureNumberProperty = serializedObject.FindProperty(nameof(BlurReferenceProvider.featureNumber));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        UIBlurEditor.DrawBlurCommonPropertiesOne(null, cameraProperty, featureNumberProperty, go, nameof(BlurReferenceProvider));
        serializedObject.ApplyModifiedProperties();
    }
}
}
