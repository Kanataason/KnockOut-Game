using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UiSelectInfoTextManager : MonoBehaviour
{
    //必要なテキストステータスを宣言
    public enum SelectTextState
    {
        Selecting,
        Explanation,
        Name,
        Hp,
        Type,
        Difficulty
    }

    //どちらのプレイヤーのものかを確認するためのステータス
    public enum PlayerId
    {
        Player1 =1,
        Player2 =2
    }

    //テキストの情報を設定するためのクラス
    [Serializable]
    public class TextInfo
    {
        public PlayerId Player;
        public SelectTextState State;
        public TextMeshProUGUI SelectText;
    }

    [SerializeField] private List<TextInfo> _textList;//インスペクターで設定したテキストを管理するリスト

    //リストの情報を管理するディクショナリー
    private Dictionary<(PlayerId, SelectTextState), TextMeshProUGUI> _textDictionary= new();

    private void Start()
    {
        InitList();
    }
    public void InitList()//Listの初期化
    {
        _textDictionary.Clear();
        //リストの情報をディクショナリーにセット
        foreach (var text in _textList)
        {
            _textDictionary[(text.Player, text.State)] = text.SelectText;
        }
    }

    //textに表示させる
    public void SetText(int player, SelectTextState state, string baseText)
    {
        var text = Get((PlayerId)player+1, state);//引数のステータスとプレイヤーのナンバーで紐づいているテキストを取得

        if (text == null) return;

        text.text = baseText;//テキストに文を反映
    }

    //テキストを取得
    private TextMeshProUGUI Get(PlayerId player, SelectTextState state)
    {
        ////引数のステータスとプレイヤーのナンバーで紐づいているテキストを取得
        if (_textDictionary.TryGetValue((player, state), out var text))
        {
            return text;
        }

        return null;
    }
}
