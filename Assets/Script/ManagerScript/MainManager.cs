using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class MainManager : MonoBehaviour
{

    //すべてのプレイヤーを保存している
    private List<UiInputManager> playerInputManagerList = new();

    //最後に決定・キャンセルを処理したフレーム
    //1つのキーボードを2人で使っていると同じ入力が2人分届くので、1フレームに1回だけ処理する
    //（2回処理するとポーズが開いてすぐ閉じる）
    private int lastSubmitFrame = -1;
    private int lastCancelFrame = -1;
    private void OnDisable()
    {
        UnSubscribeEvents();
        UnSucscribeUiEvent();
    }
    private void UnSucscribeUiEvent()
    {
        //保存されているプレイヤーのイベント解除
        foreach (var input in playerInputManagerList)
        {
            input.OnCancelAction -= OnCancel;
            input.OnSubmitAction -= OnSubmit;
        }
        playerInputManagerList.Clear();
    }
    /// <summary>
    /// 各マネージャーのイベントを解除
    /// </summary>
    private void UnSubscribeEvents(UiCharacterSelectMenu selectManager = null)
    {
        if (InputUserMakeManager.Instance != null)
        {
            InputUserMakeManager.Instance.OnCreateCharacter -= SubscribeUiEvents;
        }
        if (PlayerInitializeManager.Instance != null)
            PlayerInitializeManager.Instance.OnEnableUiPlayerInputAction -= SubscribeUiEvents;

        if (BattleStateManager.Instance != null)
            BattleStateManager.Instance.OnNextRoundAction -= UpdateFadeByUiState;

        if (selectManager == null) return;

        selectManager.OnCancelInputAction -= UnSubscribeEvents;
    }

    //このスクリプトが生成された時に参照を渡される
    public void SubscribeEvents(UiCharacterSelectMenu selectManager)//イベント登録
    {
        selectManager.OnInputAction += SubscribeUiEvents;
        selectManager.OnCancelInputAction += UnSubscribeEvents;
    }

    /// <summary>
    /// 各マネージャーのイベントを登録する
    /// </summary>
    private void SubscribeEvents()
    {
        //Startの順番に関係なく登録できるように、通知の登録と一緒にすでに生成されているものも登録する
        if (InputUserMakeManager.Instance != null)
        {
            InputUserMakeManager.Instance.OnCreateCharacter += SubscribeUiEvents;

            var currentUiPlayer = InputUserMakeManager.Instance.GetCurrentUiPlayerInput();
            if (currentUiPlayer != null) SubscribeUiEvents(currentUiPlayer);
        }

        if (PlayerInitializeManager.Instance != null)
        {
            PlayerInitializeManager.Instance.OnEnableUiPlayerInputAction += SubscribeUiEvents;

            foreach (var player in PlayerInitializeManager.Instance.GetPlayersList())
                SubscribeUiEvents(player);
        }

        if (BattleStateManager.Instance != null)
            BattleStateManager.Instance.OnNextRoundAction += UpdateFadeByUiState;
    }

    /// <summary>
    /// プレイヤーの入力イベントを登録
    /// </summary>
    /// <param name="input"></param>
    private void SubscribeUiEvents(PlayerInput input)
    {
        if (input == null) return;

        var uiManager = input.GetComponent<UiInputManager>();
        if (uiManager == null) return;

        //一回解除してから設定
        uiManager.OnCancelAction -= OnCancel;
        uiManager.OnSubmitAction -= OnSubmit;

        uiManager.OnCancelAction += OnCancel;
        uiManager.OnSubmitAction += OnSubmit;

        if (!playerInputManagerList.Contains(uiManager))
            playerInputManagerList.Add(uiManager);
    }




    private void Start()
    {

        SubscribeEvents();

        UpdateFadeByUiState();

    }

    /// <summary>
    /// 現在のUI状態を確認して必要なフェード演出を開始する
    /// </summary>
    private void UpdateFadeByUiState()
    {
        float durationTime = 0;
        float startAlpha = 1;
        float endAlpha = 0;


        float waitLoadTime = 0.3f;
        //引数の数字だけ待つ
        Delay.WaitTime(this, waitLoadTime, () => 
        {
            //現在のUiステータスをみてフェードさせる
            switch (GameManager.Instance.CurrentUiState)
            {

                case UiState.SelectCharacter:
                    durationTime = 0.5f;
                    EventManager.Instance.Startfade(durationTime, startAlpha, endAlpha);
                    break;

                case UiState.Battle:
                    durationTime = 1f;
                    EventManager.Instance.Startfade(durationTime, startAlpha, endAlpha);
                    break;
                default: break;
            }
        });
    }
    private void OnSubmit()//プレイヤーが決定を押したら流れる
    {
        if (lastSubmitFrame == Time.frameCount) return;
        lastSubmitFrame = Time.frameCount;

        //バトル中だけ使用
        if (GameManager.Instance.CurrentUiState is UiState.Pause or UiState.Battle)
        {
            var currentSelectButton = EventSystem.current.currentSelectedGameObject;
            //現在選んでいるボタンがあれば流す
            if(currentSelectButton != null)
            {
                var button = currentSelectButton.GetComponent<Button>();
                if (button == null) return;

                button.onClick.Invoke();//選んでいるボタンを実行
            }
        }
    }
    private void OnCancel()//プレイヤーがキャンセルを押したら流れる
    {
        if (lastCancelFrame == Time.frameCount) return;
        lastCancelFrame = Time.frameCount;

        EventManager.Instance.EscCancel();
    }

}
