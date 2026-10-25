using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class UiTextMover : MonoBehaviour
{
    public enum TextPosition
    {
        First,
        Second,
        Other,
        None
    }
    // 表示対象のTMPテキスト
    [SerializeField] public TMP_Text text;

    // 非表示時の色(a=0)
    [SerializeField] private Color NormalColor;

    // 表示時の色
    [SerializeField] private Color EnableColor;

    // 拡大アニメーション時間
    [SerializeField] private float Duration = 0.5f;

    // 文字毎の表示間隔
    [SerializeField] private float Interval = 0.05f;

    // 表示後に待機する時間
    [SerializeField] private float waitTime = 2;

    // 拡大時の目標スケール
    [SerializeField] private Vector3 targetPos;

    // テキストの順序
    [SerializeField] private TextPosition textPosition = TextPosition.None;

    private Vector3 normalSize;

    private void Start()
    {
        InitTextInfo();
        SubscribeEvents();
    }
    private void OnDisable()
    {
        UnSubscribeEvents();
    }
    private void SubscribeEvents()//イベント登録
    {
        BattleStateManager.Instance.OnNextRoundAction += InitTextInfo;
    }
    private void UnSubscribeEvents()//イベント解除
    {
        if(BattleStateManager.Instance != null)
        BattleStateManager.Instance.OnNextRoundAction -= InitTextInfo;
    }
    public TextPosition GetTextPosition() => textPosition;
    private void InitTextInfo()
    {

        normalSize = transform.localScale;
        // 初期状態は透明にしておく
        SetTextColor(text, NormalColor);
    }


    /// <summary>
    /// テキスト全体の頂点カラーを変更する
    /// </summary>
    private void SetTextColor(TMP_Text text, Color color)
    {
        text.ForceMeshUpdate();

        var info = text.textInfo;
        Color32[] colors = info.meshInfo[0].colors32;
        Color32 targetColor = color;

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = targetColor;
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

    }

    /// <summary>
    /// テキスト出現演出
    /// スケールを拡大しながら表示する
    /// </summary>
    public IEnumerator FadeText()
    {

        // 一度透明状態に戻す
        SetTextColor(text, NormalColor);

        Vector3 initScale = text.transform.localScale;

        float elapsed = 0;

        while (elapsed < Interval)
        {
            float t = elapsed / Interval;

            text.transform.localScale =
                Vector3.Lerp(initScale, targetPos, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        text.transform.localScale = targetPos;

        text.ForceMeshUpdate();
        // 表示状態を少し維持
        yield return new WaitForSeconds(Duration);
    }


    /// <summary>
    /// 中央の文字から順番に消していく演出
    /// </summary>
    public IEnumerator StartFade()
    {
        TMP_TextInfo info = text.textInfo;

        var charInfo = info.characterInfo;
        int charCount = info.characterCount;

        // 表示されている文字の中心位置を求める
        float totalX = 0;
        int visibleCount = 0;

        for (int i = 0; i < charCount; i++)
        {
            if (!charInfo[i].isVisible)
                continue;

            totalX += charInfo[i].origin;
            visibleCount++;
        }

        float center = totalX / visibleCount;

        // 中央に近い順に並べる
        List<int> order = new();

        for (int i = 0; i < charCount; i++)
        {
            if (charInfo[i].isVisible)
                order.Add(i);
        }

        order = order
            .OrderBy(i => Mathf.Abs(charInfo[i].origin - center))
            .ToList();

        yield return new WaitForSeconds(waitTime);

        // 中央から外側へ向けてフェード
        foreach (var index in order)
        {
            StartCoroutine(EndFade(index, Duration, info));

            yield return new WaitForSeconds(Interval);
        }
    }

    /// <summary>
    /// 指定文字を縮小＋透明化する
    /// </summary>
    IEnumerator EndFade(int index, float duration, TMP_TextInfo textInfo)
    {
        text.ForceMeshUpdate();

        var vertices = textInfo.meshInfo[0].vertices;
        var colors = textInfo.meshInfo[0].colors32;

        var charInfo = textInfo.characterInfo[index];
        int vertexIndex = charInfo.vertexIndex;

        // 元頂点を保存
        Vector3[] originalPos = new Vector3[4];

        for (int i = 0; i < 4; i++)
        {
            originalPos[i] = vertices[vertexIndex + i];
        }

        // 文字中心を求める
        Vector3 centerR = (originalPos[2] + originalPos[2]) / 2;
        Vector3 centerL = (originalPos[0] + originalPos[0]) / 2;
        centerL += centerR;

        float elapsed = 0;
        float fadeTime = duration * 0.8f;

        while (elapsed < fadeTime)
        {
            float t = elapsed / fadeTime;

            float scale = Mathf.Lerp(1f, 0.8f, t * 2);

            byte alpha =
                (byte)Mathf.Lerp(NormalColor.a, 0, t * 4);

            for (int i = 0; i < 4; i++)
            {
                Vector3 offset =
                    vertices[vertexIndex + i] - centerL;

                vertices[vertexIndex + i] =
                    centerL + offset * scale;

                colors[vertexIndex + i].a = alpha;
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 完全に消す
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset =
                vertices[vertexIndex + i] - centerL;

            vertices[vertexIndex + i] =
                centerL + offset * 0.5f;

            colors[vertexIndex + i].a = 0;
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }


    /// <summary>
    /// KO表示アニメーション
    /// </summary>
    public IEnumerator AnimationStartDeadText()
    {
        float scaleOffset = 2;

        text.ForceMeshUpdate();

        yield return null;

        SetTextColor(text, EnableColor);

        Vector3 scale = text.transform.localScale;

        float interval = 0.05f;
        float elapsed = 0;

        while (elapsed < interval)
        {
            float t = elapsed / interval;

            text.transform.localScale =
                Vector3.Lerp(scale, targetPos * scaleOffset, t);

            elapsed += Time.deltaTime;

            yield return null;
        }

        text.transform.localScale = scale;

        float waitTime = 1;
        yield return new WaitForSeconds(waitTime);
    }

    /// <summary>
    /// KO演出終了処理
    /// </summary>
    public IEnumerator AnimationEndDeadText()
    {
        float scaleOffset = 2;
        text.ForceMeshUpdate();

        SetTextColor(text, EnableColor);

        Vector3 scale = text.transform.localScale;

        float elapsed = 0;

        while (elapsed < Interval)
        {
            float t = elapsed / Interval;

            text.transform.localScale =
                Vector3.Lerp(scale, targetPos * scaleOffset, t);

            elapsed += Time.deltaTime;

            yield return null;
        }

        text.transform.localScale = scale;
        float waitTime = 2;
        yield return new WaitForSeconds(waitTime);
    }
    public void InitTextColor()//色を透明にする
    {
        SetTextColor(text, NormalColor);
    }
    /// <summary>
    /// 勝利演出 終わったら戻る（リザルトの表示はBattleStateManagerが行う）
    /// </summary>
    public IEnumerator PlayWinText()
    {

        text.ForceMeshUpdate();

        SetTextColor(text,Color.yellow);

        Vector3 scale = text.transform.localScale;
        float elapsed = 0;
        while (elapsed < Interval)
        {
            var t = elapsed / Interval;
            text.transform.localScale = Vector3.Lerp(scale, targetPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        text.transform.localScale = scale;

        float waitTime = 3;
        yield return new WaitForSeconds(waitTime);

        text.text = "";
    }
}