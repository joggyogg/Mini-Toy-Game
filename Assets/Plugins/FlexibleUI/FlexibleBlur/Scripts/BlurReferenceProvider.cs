using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace JeffGrawAssets.FlexibleUI
{
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class BlurReferenceProvider : MonoBehaviour
{
    public Camera cameraReference;
    public int featureNumber;

    public static Dictionary<Canvas, (Camera camera, int featureNumber)> cameraReferenceDict = new ();

    private Canvas _canvas;
    private Canvas canvas
    {
        get
        {
            if (_canvas == null)
                CacheCanvas();
            return _canvas;
        }
    }

    private void CacheCanvas()
    {
        var list = ListPool<Canvas>.Get();
        gameObject.GetComponentsInParent(false, list);
        if (list.Count > 0)
        {
            for (int i = 0; i < list.Count; ++i)
            {
                if (list[i].isActiveAndEnabled)
                {
                    _canvas = list[i];
                    break;
                }

                if (i == list.Count - 1)
                    _canvas = null;
            }
        }
        else
        {
            _canvas = null;
        }

        ListPool<Canvas>.Release(list);
    }

    void OnEnable() => UpdateDict();
    void OnDisable() => cameraReferenceDict.Remove(canvas);
    void Update() => UpdateDict();
    void UpdateDict() => cameraReferenceDict[canvas] = (cameraReference, featureNumber);
}
}