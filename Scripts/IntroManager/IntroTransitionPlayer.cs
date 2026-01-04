using UnityEngine;
using UnityEngine.UI;

public class IntroTransitionPlayer : MonoBehaviour
{
    private GameObject transitionObj;
    private Material instanceMaterial;

    public void CreatePixelatedOverlay(Shader wipeShader, Color color, float blockCount = 12f, float slope = 0.6f)
    {
        Canvas displayCanvas = GameObject.Find("Display Canvas")?.GetComponent<Canvas>();
        if (displayCanvas == null) displayCanvas = FindFirstObjectByType<Canvas>();

        transitionObj = new GameObject("Temp_Pixelated_Intro");
        transitionObj.transform.SetParent(displayCanvas.transform, false);
        transitionObj.transform.SetAsLastSibling();

        Image img = transitionObj.AddComponent<Image>();
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.sizeDelta = Vector2.zero;

        instanceMaterial = new Material(wipeShader);
        instanceMaterial.SetColor("_Color", color);
        instanceMaterial.SetFloat("_BlockCount", blockCount);
        instanceMaterial.SetFloat("_Slope", slope);

        instanceMaterial.SetFloat("_Invert", 1.0f);
        instanceMaterial.SetFloat("_Progress", 0f);

        img.material = instanceMaterial;
    }

    public void UpdateProgress(float progress)
    {
        if (instanceMaterial != null)
            instanceMaterial.SetFloat("_Progress", progress);
    }

    public void Cleanup()
    {
        if (transitionObj != null) Destroy(transitionObj);
        if (instanceMaterial != null) Destroy(instanceMaterial);
    }
}