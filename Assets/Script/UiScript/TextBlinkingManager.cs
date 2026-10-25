using TMPro;
using UnityEngine;

public class TextBlinkingManager : MonoBehaviour
{
    [Header("Reference")]
    private  TextMeshProUGUI titleText;

    [SerializeField] private TitleManager titleManager;
    [SerializeField] private float blinkSpeed = 2;//ブリンクの強さ

    private bool hasStartedTitle = false;

    private void OnEnable()//イベントを登録
    {
        if(titleManager != null)
        titleManager.OnTitleInput += OnTitleStarted;
    }
    private void OnDisable()//イベントを解除
    {
        if (titleManager != null)
        titleManager.OnTitleInput -= OnTitleStarted;
    }
    private void Start()
    {
        titleText = GetComponentInChildren<TextMeshProUGUI>();
    }

    void Update()
    {
        //入力されていたら流す
        if (hasStartedTitle) return;

        Color color = titleText.color;//テキストの色抽出

        // α値をサイン波で変化させて点滅させる
        color.a = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed));
        titleText.color = color;
    }

    //タイトル画面で入力されたら停止する
    private void OnTitleStarted()
    {
        hasStartedTitle = true;
    }
}
