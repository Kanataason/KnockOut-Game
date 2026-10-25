using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerSideSelectCursor : MonoBehaviour
{
    //自分の番号を選択
    [Header("Main")]
    public int Id;

    [Header("Refarence")]
    public RectTransform[] ButtonPos;//プレイヤーの移動場所のリスト

    public Button[] ButtonList;//選択時のボタンリスト

    public UiInputManager InputManager;//プレイヤーの入力スクリプト

    private PlayerInput _myPlayerInput;

    private int currentindex = 1;//現在自分がいるボタンのindex
    private bool isFirst = false;//最初の処理かをみるフラグ

    private bool isSelected = false;//選択済みかを確認するフラグ

    public event  Action<int,PlayerInput> OnSubmitButton;//決定ぞボタンを押したら呼ばれるアクション
    public event Action<PlayerSideSelectCursor> OnCancelAction;//キャンセルボタンを押したら呼ばれるアクション

    void Start()
    {
        Initialize();
        TryGetComponents();
        SubscribeEvents();
    }
    private void OnDestroy()//イベントの解除
    {
        OnCancelAction?.Invoke(this);

        InputManager.OnCancelAction -= CheckCancel;
        InputManager.OnSubmitAction -= SubmitUpdate;
    }
    private void SubscribeEvents()//イベントの登録
    {
        InputManager.OnCancelAction += CheckCancel;
        InputManager.OnSubmitAction += SubmitUpdate;
    }
    private void TryGetComponents()//コンポーネントを取得
    {
        _myPlayerInput = GetComponent<PlayerInput>();
    }
    public void Initialize()//初期化
    {
        //フラグの初期化
        isFirst = false;
        SetSelected(false);

        currentindex = 1;//indexを初期化
        transform.position = ButtonPos[currentindex].position;//場所を初期化
    }

    void Update()
    {
        if (isSelected) return;

        NavigateUpdate();
    }
    private void CheckCancel()//キャンセルボタンを押したら流れる
    {
        SetSelected(false);
        EventManager.Instance.EscCancel();
    }
    private void NavigateUpdate()//ボタンの移動処理
    {
        //現在の入力を取得
        float axis = InputManager.Navigate.x;
        float deadZone = 0.3f;

        if (Mathf.Abs(axis) < deadZone)//動かしていなかったら流れる
        {
            isFirst = false;
        }
        if (isFirst) return;

        if(axis != 0)//動かしていたら
        {
            //入力が右か左かを見ている
            currentindex = axis < -0.5f ? Mathf.Max(0, currentindex - 1) :
                            Mathf.Min(ButtonPos.Length - 1, currentindex + 1);
            //自分の位置を変更する
            transform.position = ButtonPos[currentindex].position;
            isFirst = true;
        }

    }
    private void SubmitUpdate()//決定ボタンを押したら処理
    {
        if (!isSelected)
        {
            if (CheckSelected() == false) return;
            if (ButtonList[currentindex] == ButtonList[1]) return;

            SetSelected(true);
            var resultIndex = Mathf.Clamp(currentindex, 0, 1);
            //すでに選択済みかの確認を通知
            OnSubmitButton?.Invoke(resultIndex, _myPlayerInput);

            //0.3秒待つ
            Delay.WaitTime(this, 0.3f, CheckSelectData);
        }
    }
    private void CheckSelectData()
    {
        var p1ID = PlayerDataManager.Instance.Player1Data.PlayerId;
        var p2ID = PlayerDataManager.Instance.Player2Data.PlayerId;

        int InvalidPlayerId = -1;
        int Player1Id = 0;
        int Player2Id = 1;

        //ソロプレイモードなら流れる
        if (GameManager.Instance.CurrentMode is GameManager.PlayMode.Solo)
        {
            //どちら側に設定したかを確認
            if (p1ID == InvalidPlayerId)
                PlayerDataManager.Instance.Player1Data.PlayerId = Player1Id;

            if (p2ID == InvalidPlayerId)
                PlayerDataManager.Instance.Player2Data.PlayerId = Player2Id;

            ButtonList[currentindex].onClick?.Invoke();//ボタンに設定してあるイベントを実行
        }
        else
        {
            // 両方が設定されたら実行
            if (p1ID != InvalidPlayerId && p2ID != InvalidPlayerId)
            {
                ButtonList[currentindex].onClick?.Invoke();//ボタンに設定してあるイベントを実行
            }
        }
    }
    private bool CheckSelected()//現在のボタンの場所が選択済みか確認関数
    {
        var p1ID = PlayerDataManager.Instance.Player1Data.PlayerId;
        var p2ID = PlayerDataManager.Instance.Player2Data.PlayerId;


        int slotId = Mathf.Clamp(currentindex, 0, 1);

        //もう選択されていたらfalseを変えす
        if(p1ID == slotId||p2ID  == slotId)
        {
            return false;
        }
        return true;

    }
    private void SetSelected(bool IsSelected = true)//フラグの変更
    {
        isSelected = IsSelected;
    }
}
