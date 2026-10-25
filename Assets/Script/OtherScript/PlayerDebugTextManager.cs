using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PlayerDebugTextManager : MonoBehaviour
{
    [Serializable]
    private class DebugTextData
    {
        public TextMeshProUGUI Text;
        public Func<string> Getter;
    }

    [FormerlySerializedAs("debagPanel")] [SerializeField] private GameObject debugPanel;



    private List<TextMeshProUGUI> defaultTexts = new();

    private Dictionary<string, DebugTextData> debugTextList = new();

    [SerializeField] private bool isActive = false;
    private int currentTextIndex = 0;

    [SerializeField] private TextMeshProUGUI currentText;
    [SerializeField] private TextMeshProUGUI targetText;

    private int startUpFrame;
    private int activeFrame;
    private int totalFrame;

    private int StartupLength => startUpFrame;
    private int ActiveLength => activeFrame - startUpFrame;
    private int RecoveryLength => totalFrame - activeFrame;

    private List<int> frameList = new();

    [SerializeField] private Image startUpImage;
    [SerializeField] private Image activeImage;
    [SerializeField] private Image recoveryImage;
    private void OnDestroy()
    {
        debugTextList.Clear();
        defaultTexts.Clear();

        currentTextIndex = 0;
    }

    private void Start()
    {
        var texts = GetComponentsInChildren<TextMeshProUGUI>();
        foreach(var text in texts)
        {
            defaultTexts.Add(text);
        }
    }
    /// <summary>
    /// デバッグ表示を登録
    /// </summary>
    public void Register(string label, Func<string> getter)
    {
        if (debugTextList.ContainsKey(label))
        {
            Debug.LogWarning($"{label} は既に登録されています");
            return;
        }

        if (currentTextIndex >= defaultTexts.Count)
        {
            Debug.LogWarning("登録可能なテキスト数を超えました");
            return;
        }

        debugTextList.Add(label, new DebugTextData
        {
            Text = defaultTexts[currentTextIndex],
            Getter = getter
        });

        currentTextIndex++;
    }
    public void SetEndText()
    {
        debugPanel.SetActive(false);
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            isActive = !isActive;

            if (debugPanel != null)
            debugPanel.SetActive(isActive);
        }

        if (!isActive) return;

        foreach (var data in debugTextList)
        {
            data.Value.Text.text =
                $"{data.Key} : <color=red>{data.Value.Getter()}</color>";
        }
    }

    
    public void SetAttackDataInfo(ActionFrameData data)
    {
        frameList.Clear();
        if (data.ActionName is AnimationManager.AnimationId.Smash) return;

        startUpFrame = data.frameData[1].FrameNumber;
        activeFrame = data.frameData[2].FrameNumber;
        totalFrame = data.frameData[^1].FrameNumber;
    }
    public void UpdateFrame(int currentIndex)
    {
        float widthPerFrame = 15f;
        float height = 60;
        // Startup
        int startup = Mathf.Clamp(currentIndex, 0, startUpFrame);
        startUpImage.rectTransform.sizeDelta =
            new Vector2(startup * widthPerFrame, height);

        // Active
        int active = Mathf.Clamp(currentIndex - startUpFrame, 0, ActiveLength);
        activeImage.rectTransform.sizeDelta =
            new Vector2(active * widthPerFrame, height);

        // Recovery
        int recovery = Mathf.Clamp(currentIndex - activeFrame, 0, RecoveryLength);
        recoveryImage.rectTransform.sizeDelta =
            new Vector2(recovery * widthPerFrame, height);

        var targetFrame = targetText.text;
        currentText.text = $"{currentIndex.ToString()}/{targetFrame}";
    }
    public void HitUpdateFrame(int currentIndex)
    {
        currentText.text = currentIndex.ToString();
    }

}
