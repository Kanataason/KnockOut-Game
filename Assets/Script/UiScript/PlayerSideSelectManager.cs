using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSideSelectManager : MonoBehaviour
{
    /// <summary>
    /// 1p,2p選択時の初期化処理をするスクリプト
    /// </summary>
    [Header("List")]
    //プレイヤー１のポジションのリスト
    [SerializeField] private List<RectTransform> _player1ButtonPosList = new List<RectTransform>();
    //プレイヤー２のポジションのリスト
    [SerializeField] private List<RectTransform> _player2ButtonPosList = new List<RectTransform>();
    //洗濯用のボタンを要れるリスト
    [SerializeField] private List<UnityEngine.UI.Button> _buttonList = new List<UnityEngine.UI.Button>();


    private void OnEnable()
    {
        SubscribeEvent();
    }
    private void OnDisable()
    {
        UnSubscribeEvent();
    }
    /// <summary>
    /// 各種マネージャーのイベントを登録
    /// </summary>
    private void SubscribeEvent()
    {
        InputUserMakeManager.Instance.OnSetList += SetList;
        EventManager.Instance.OnProcessedAction += SetPlayerInfo;
    }
    /// <summary>
    /// 各種マネージャーのイベントを解除
    /// </summary>
    private void UnSubscribeEvent()
    {
        if (EventManager.Instance != null)
            EventManager.Instance.OnProcessedAction -= SetPlayerInfo;

        InputUserMakeManager.Instance.OnSetList -= SetList;
    }

    //-----------送られてきたプレイヤーをイベント解除、登録
    private void SubscribePlayerEvents(PlayerSideSelectCursor player)
    {
        player.OnSubmitButton += CheckPlayer;
        player.OnCancelAction += UnSubscribePlayerEvents;
    }
    private void UnSubscribePlayerEvents(PlayerSideSelectCursor player)
    {
        player.OnSubmitButton -= CheckPlayer;
        player.OnCancelAction -= UnSubscribePlayerEvents;
    }

    public void SetPlayerInfo()//キャラクターの位置を決めるときのUIを生成する
    {
        InputUserMakeManager.Instance.SetPlayerUser();
    }
    /// <summary>
    /// 生成したコントローラーにUiの情報を渡す
    /// </summary>
    private void SetList(PlayerSideSelectCursor moveUiPlayer, int Id)
    {
        var list = Id == 0 ? _player1ButtonPosList : _player2ButtonPosList;

        moveUiPlayer.transform.position = list[1].transform.position;
        //プレイヤーに合ったボタンの情報をプレイヤーに渡す
        for(int i= 0;i<moveUiPlayer.ButtonPos.Length; i++)
        {
            moveUiPlayer.ButtonPos[i] = list[i];
            moveUiPlayer.ButtonList[i] = _buttonList[i];
        }

        SubscribePlayerEvents(moveUiPlayer);//イベントの登録
    }

    //------------------------------
    /// <summary>
    /// 順番がすでに割り当てわれているか確認
    /// </summary>
    private void CheckPlayer(int Id,PlayerInput input)
    {
        int p1ID = PlayerDataManager.Instance.Player1Data.PlayerId;
        int p2ID = PlayerDataManager.Instance.Player2Data.PlayerId;

        int assignedID = PlayerIds.None;

        // 1Pが空
        if (p1ID == PlayerIds.None)
        {
            assignedID = Id;
        }
        // 2Pが空
        else if (p2ID == PlayerIds.None)
        {
            assignedID = Id;
        }
        // どちらかに割り当て
        if (assignedID != PlayerIds.None)
        {
            //プレイヤーデータに情報を渡してセット
            PlayerDataManager.Instance.SetPlayerInfo(
                assignedID,input.currentControlScheme,
                input.devices[0]);
        }

    }

}
