using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Users;

public class InputUserMakeManager : MonoBehaviour
{
    /// <summary>
    /// Ui操作の入力を設定するスクリプト
    /// </summary>
    
    //操作するプレイヤーのステータスを宣言
    enum DeviceState
    {
        Ui,
        Select
    }


    public static InputUserMakeManager Instance { get; private set; }

    //シーンに一つしかないことを保証
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }


    private List<PlayerInput> playerList = new List<PlayerInput>();//操作デバイスリスト
    private UiInputManager currentUiController = null;//現在操作デバイス
    private PlayerInput currentUiDevice = null; //現在操作デバイスのコンポーネント

    //------------イベントの解除＆登録
    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }
    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    [Header("InstanceChara")]
    [SerializeField] private GameObject mainUiInputController;//メインのUi操作プレハブ

    //1P,2P選択時の操作プレハブ
    [SerializeField] private GameObject player1InputController;
    [SerializeField] private GameObject player2InputController;

    [Header("Refarence")]
    [SerializeField] private InputSystemUIInputModule uIInputModules;//Uiコントローラー

    [Header("Parent")]
    //1P,2P選択時の親オブジェクトの場所
    [SerializeField] private Transform setPlayerParent;
    [FormerlySerializedAs("setCharactorParent")] [SerializeField] private Transform setCharacterParent;


    private DeviceState currentDeviceState = DeviceState.Ui;//現在の生成ステータス
    private bool hasTwoGamepads = false;//接続されているコントローラー確認フラグ

    public event Action<PlayerInput> OnCreateCharacter;//生成したキャラクターを渡すアクション
    public event Action<PlayerSideSelectCursor, int> OnSetList;//操作デバイスを渡して情報をセットするためのアクション
     void Start()
    {
        RemoveUser();//現在設定されている操作デバイスを解除
        InputUser.listenForUnpairedDeviceActivity = 0;//自動ペアリングを停止

        LoadUiUser();
    }
    //Ui操作用のデバイスを設定
    //通知を受け取る側は登録時に今の操作デバイスも取得するので、時間を待たずにすぐ生成する
    private void LoadUiUser()
    {
        ActiveUiInputModule();

        CheckDeviceController();//現在選択されているデバイスチェック

        UiSetUser();//操作デバイスの設定
    }
    private void ActiveUiInputModule(bool isActive = false)
    {
        uIInputModules.enabled = isActive;
    }

    private void CheckDeviceController()//現在接続されているコントローラーを見る
    {
        int Count = Gamepad.all.Count;

        if(Count == 0||!GameManager.Instance.IsSoloKeyboardMode)
        {
            return;
        }

        hasTwoGamepads = Count > 1;
    }
 

    //---------------------------------
    public void CreateUser()//現在のステータスを見て生成する
    {
        switch (currentDeviceState)
        {
            case DeviceState.Ui: 
                CreateUiInput(); break;
            case DeviceState.Select:
                CheckSelectInput(); break;
        }


    }
    #region
    private void CreateUiInput()//UIを動かすcontroller生成
    {
        // 既に存在するなら再利用
        if (currentUiController != null)
        {
            //コントローラーが二台以上接続されているなら流す
            if(Gamepad.all.Count > 1)
            {
                currentUiDevice.SwitchCurrentControlScheme(
    "GamePad", Gamepad.all[0]);
            }
            //ゲームパッドが一台以上接続とソロキーボードモードがOffなら流す
            else if (Gamepad.all.Count > 0 && !GameManager.Instance.IsSoloKeyboardMode)
            {
                currentUiDevice.SwitchCurrentControlScheme(
                    "GamePad", Gamepad.all[0]);
            }
            else
            {
                currentUiDevice.SwitchCurrentControlScheme(
                    "KeyBoard",
                    Keyboard.current);
            }

            return;
        }

        PlayerInput player;
        //コントローラーが二台以上接続されているなら流す
        if (Gamepad.all.Count > 1)
        {
            player = InstantiatePlayer(mainUiInputController, "GamePad", Gamepad.all[0], true);
        }
        //ゲームパッドが一台以上接続とソロキーボードモードがOffなら流す
        else if (Gamepad.all.Count > 0 && !GameManager.Instance.IsSoloKeyboardMode)
        {
            player = InstantiatePlayer(mainUiInputController, "GamePad", Gamepad.all[0], true);
        }
        else
        {
            player = hasTwoGamepads
                ? InstantiatePlayer(mainUiInputController, "GamePad", Gamepad.all[0], true)
                : InstantiatePlayer(mainUiInputController, "KeyBoard", Keyboard.current, true);
        }

        //現在操作デバイスに設定
        currentUiDevice = player;

        //Uiコントローラーを設定
        player.uiInputModule = uIInputModules;
        OnCreateCharacter?.Invoke(player);//生成したことを通知
    }
    private void CheckSelectInput()//1p,2p選択用の操作デバイスを生成
    {
        //何のデバイスも接続されてなければ流す
        if (Gamepad.all.Count == 0)
        {
            InstantiatePlayer(player1InputController, "KeyBoard", Keyboard.current);
            InstantiatePlayer(player2InputController, "", null);
            //ソロプレイに変える
            GameManager.Instance.ChangePlayMode(GameManager.PlayMode.Solo);

            SetParent(playerList);//親を設定
            return;
        }


         int MinPlayerCount = 2;
        //接続デバイスが一台とソロキーボードモードがOffなら流す
        if (Gamepad.all.Count<MinPlayerCount&&!GameManager.Instance.IsSoloKeyboardMode)
        {
            InstantiatePlayer(player1InputController, "GamePad", Gamepad.all[0]);
            InstantiatePlayer(player2InputController, "", null);
            //ソロプレイに変える
            GameManager.Instance.ChangePlayMode(GameManager.PlayMode.Solo);

            SetParent(playerList);//親を設定
            return;
        }
        //接続デバイスが二台なら流す
        CreateUiController();
        //二人プレイに変える
        GameManager.Instance.ChangePlayMode(GameManager.PlayMode.Multi);
    }
    // 二人プレイのときに呼ばれる関数
    private void CreateUiController()//デバイスを生成
    {
        //接続されているコントローラーの数でデバイスを決める
        InputDevice device1 = hasTwoGamepads ? Gamepad.all[0] : Keyboard.current;
        InputDevice device2 = hasTwoGamepads ? Gamepad.all[1] : Gamepad.all[0];

        var scheme1 = hasTwoGamepads ? "GamePad" : "KeyBoard";
        var scheme2 = "GamePad";

        //生成
        InstantiatePlayer(player1InputController, scheme1, device1);
        InstantiatePlayer(player2InputController, scheme2, device2);

        SetParent(playerList);//親を設定
    }
    #endregion

    //引数1 インスタンス　2 デバイスの名前 3 操作デバイス 4 Ui用のPlayerinputかどうかのflag
    private PlayerInput InstantiatePlayer(GameObject playerInstance,string deviceName,InputDevice device,bool IsUiController = false)
    {
        PlayerInput player = null;
        //デバイスがnullじゃなかったら流す
        if (device != null)
        {
            player = PlayerInput.Instantiate(playerInstance, controlScheme: deviceName, pairWithDevice: device);
        }
        else
        {
            player = PlayerInput.Instantiate(playerInstance);
            player.enabled = false;
        }

        if (player == null) return null;

        SetPlayerInfo(player,deviceName,IsUiController);//操作デバイスの情報を設定
        return player;
    }

    //入力専用のスクリプトを取得してリストに追加　操作ステータスを登録
    private void SetPlayerInfo(PlayerInput input,string deviceName,bool IsUiController)
    {
        //Uiのコントローラーなら現在の操作デバイスに設定 無ければリストに追加
        if (IsUiController) currentUiController = input.GetComponent<UiInputManager>();
        else playerList.Add(input);

        //デバイスの名前と操作タイプを設定
        input.defaultControlScheme = deviceName;
        input.SwitchCurrentControlScheme();
    }

    public void UiSetUser()//gamemenu時に呼ばれる
    {
        ChangeState(DeviceState.Ui);//生成ステータスをUiに切り替え

        CharacterRemoveAndCreate();

        ActiveUiInputModule(true);
    } 
    private void CharacterRemoveAndCreate()
    {
        BreakAndRemoveUser();//設定されている操作デバイスを解除
        CreateUser();//操作デバイスを設定
    }
    //--------------------------

    public void SetPlayerUser()//playerset時に呼ばれる用
    {
        ChangeState(DeviceState.Select); //生成ステータスをSelectに切り替え

        CharacterRemoveAndCreate();
    }
    private void SetParent(List<PlayerInput> inputList)//UIの親を設定する
    {
        for(int i = 0; i < inputList.Count; i++)
        {
            var move = inputList[i].GetComponent<PlayerSideSelectCursor>();
            if (move == null) continue;

            OnSetList?.Invoke(move, i);//設定開始を通知

            inputList[i].transform.SetParent(setPlayerParent,false);//親を設定
        }
    }
    //リセット関数
    public void RemoveUser()//現在登録されているUserを解除する
    {
        var allUsers = InputUser.all.ToList();
        foreach (var user in allUsers)
        {
            user.UnpairDevicesAndRemoveUser();
        }
    }
    public void BreakAndRemoveUser()//リストに入っているUserを破棄
    {
        foreach (var dev in playerList)
        {
            Destroy(dev.gameObject);
        }
        playerList.Clear();//初期化
    }
    public UiInputManager GetCurrentUiController() { return currentUiController; }//現在の操作デバイスを返す
    public PlayerInput GetCurrentUiPlayerInput() { return currentUiDevice; }//現在の操作デバイスのPlayerInputを返す


    public void ChangeController()//キーボード単体使用が変わった時に流れる
    {
        UiSetUser();//デバイスの入力を再設定
    }

    private void ChangeState(DeviceState state)//現在のUiのステータスを変更
    {
        currentDeviceState = state;
    }
    //----------コントローラーの接続確認
    void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change is InputDeviceChange.Added)
        {
            // コントローラーが接続されたとき
            if (device is Gamepad)
            {
                OnGamepadConnected(device as Gamepad);
            }
        }
        else if (change is InputDeviceChange.Removed)
        {
            //コントローラーが切断されたとき
            if (device is Gamepad)
            {
                OnGamepadDisconnected(device as Gamepad);
            }
        }
    }
    void OnGamepadConnected(InputDevice input)//新しいデバイスの入力を登録
    {
        TryCreateUser();

    }
    void OnGamepadDisconnected(InputDevice input)//新しいデバイスの入力を登録
    {
        TryCreateUser();

    }

    private void TryCreateUser()//操作デバイスを再接続
    {
        CheckDeviceController();//現在接続されているコントローラーの数を確認

        //デバイスの入力を再設定
        if (currentDeviceState is DeviceState.Ui)
            UiSetUser();
        else
            SetPlayerUser();
    }
}

