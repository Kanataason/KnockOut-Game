using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.UI;


public class PlayerInitializeManager : MonoBehaviour
{
    public static PlayerInitializeManager Instance { get; private set; }
    /// <summary>
    /// 生成が終了したと知らせるだけのスクリプト
    /// </summary>
    [SerializeField] private Transform playerPos1;
    [SerializeField] private Transform playerPos2;

    [SerializeField] private Image PlayerImage1;
    [SerializeField] private Image PlayerImage2;

    //-------refarence--------//
    [FormerlySerializedAs("UIHpBarManager")] [SerializeField] private BattleHud battleHud;
    [SerializeField] private PlayerDebugTextManager player1TextManager;
    [SerializeField] private PlayerDebugTextManager player2TextManager;

    PlayerInput Player1InputDevice;
    PlayerInput Player2InputDevice;

    private CameraFollowTwoTargets cameraFollowManager;

    private List<InputUser> _inputUsersList = new List<InputUser>();
    private List<InputDevice> _inputDevicesList = new List<InputDevice>();
    private List<PlayerInput> _playerInputList = new List<PlayerInput>();

    public event Action<PlayerInput> OnEnableUiPlayerInputAction;

    /// <summary>
    /// プレイヤーの生成と読み込みが終わってバトルを始められる状態か
    /// </summary>
    public bool IsPlayersReady { get; private set; } = false;

    //準備ができたら実行する処理
    private readonly List<Action> onPlayersReadyActions = new();

    //読み込みに失敗した時に初期化が止まり続けないための上限時間
    private const float LoadTimeout = 10f;

    private void Awake()
    {
        InputUser.listenForUnpairedDeviceActivity = 0; // 自動ペアリング無効化

        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //  private List<(PlayerInputHandler handler, PlayerInput input)> pendingHandlers = new();
    //バトルの初期化を順番に行う 時間で待たずに準備ができたかで進める
    private System.Collections.IEnumerator Start()
    {
        AudioManager.Instance.RandomBGM();
        SubscribeEvents();

        TryGetComponents();

        //1.プレイヤーを生成
        //生成の通知を受け取る側は、生成済みのプレイヤーも後から登録できるようにしている
        PlayerControllerInitialize();

        //2.キャラクターのデータとエフェクトの読み込みが終わるまで待つ
        yield return WaitPlayersLoaded();

        //3.各プレイヤーのステートマシンを初期化
        Player1InputDevice.GetComponent<PlayerController>().Initialize();
        Player2InputDevice.GetComponent<PlayerController>().Initialize();

        //4.準備ができたことを通知（ラウンド開始の演出などが始まる）
        IsPlayersReady = true;
        foreach (var action in onPlayersReadyActions)
            action?.Invoke();
        onPlayersReadyActions.Clear();
    }

    /// <summary>
    /// プレイヤーの準備ができたら実行する すでに準備ができていればすぐに実行する
    /// </summary>
    public void WhenPlayersReady(Action action)
    {
        if (IsPlayersReady)
        {
            action?.Invoke();
            return;
        }
        onPlayersReadyActions.Add(action);
    }

    //両方のプレイヤーの読み込みが終わるまで待つ
    private System.Collections.IEnumerator WaitPlayersLoaded()
    {
        float elapsed = 0f;
        while (!IsLoadFinished(Player1InputDevice) || !IsLoadFinished(Player2InputDevice))
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= LoadTimeout)
            {
                Debug.LogError("キャラクターの読み込みが終わらないのでそのまま開始します");
                yield break;
            }
            yield return null;
        }
    }
    private bool IsLoadFinished(PlayerInput player)
    {
        if (player == null) return false;

        var animation = player.GetComponent<AnimationManager>();
        var effect = player.GetComponent<PlayerEffectController>();

        //コンポーネントが無い時は待たない
        bool isAnimationLoaded = animation == null || animation.IsLoadFinished;
        bool isEffectLoaded = effect == null || effect.IsLoadFinished;

        return isAnimationLoaded && isEffectLoaded;
    }
    private void TryGetComponents()
    {
        cameraFollowManager = Camera.main.GetComponent<CameraFollowTwoTargets>();
    }

    private void OnDestroy()
    {
        ClearList();
        if (Player1InputDevice != null) Destroy(Player1InputDevice.gameObject);
        if (Player2InputDevice != null) Destroy(Player2InputDevice.gameObject);
    }
    private void OnDisable()
    {
        if (Player1InputDevice != null)
        {
            UnsubscribeEvents(Player1InputDevice);
        }
        if (Player2InputDevice != null)
        {
            UnsubscribeEvents(Player2InputDevice);
        }

    }
    private void SubscribeEvents()//イベント登録
    {
        BattleStateManager.Instance.OnUnPairUserAction +=ClearPlayerInputList;
        BattleStateManager.Instance.OnTimeOutGameAction += ClearPlayerInputList;
    }
    private void UnsubscribeEvents(PlayerInput targetinput)//イベント解除
    {
        var processController = targetinput.GetComponent<PlayerProcessController>();
        var handler = targetinput.GetComponent<PlayerInputHandler>();
        var UiInput = targetinput.GetComponent<UiInputManager>();
        
        if(targetinput.TryGetComponent<PlayerController>(out var controller))
        controller.OnRemovePos -= RemovePlayerPos;

        if (BattleStateManager.Instance != null)
        {
            BattleStateManager.Instance.OnUnPairUserAction -= ClearPlayerInputList;
            BattleStateManager.Instance.OnTimeOutGameAction -= ClearPlayerInputList;
        }

        if (handler == null) return;

        processController.UnsubscribeAttackEvent(targetinput, handler,UiInput);

        processController.UnsubscribeUiEvent(targetinput, UiInput);
    }
    //プレイヤーを生成
    private void PlayerControllerInitialize()
    {
        PlayerDataManager playerDataManager = PlayerDataManager.Instance;
        RemoveUser();
        var p1Data = playerDataManager.Player1Data;
        var p2Data = playerDataManager.Player2Data;

        Player1InputDevice = CreateDevice(p1Data.CurrentControlScheme, p1Data.CurrentDevice,p1Data.CurrentCharacter);
        Player2InputDevice = CreateDevice(p2Data.CurrentControlScheme, p2Data.CurrentDevice,p2Data.CurrentCharacter);

        Player2InputDevice.transform.rotation = Quaternion.Euler(0, -90, 0);
        Player2InputDevice.transform.position = playerPos2.position;
        Player1InputDevice.transform.position = playerPos1.position;
    }
    //セレクト画面で選択した情報でキャラクターを生成する
    private PlayerInput CreateDevice(string controlSchemeGamepad,InputDevice device,GameObject character)
    {
        PlayerInput input = PlayerInput.Instantiate(character);

        SetPlayerInput(input, controlSchemeGamepad, device);
        return input;

    }
    //入力デバイスをキャラクターと紐づけ
    private void SetPlayerInput(PlayerInput input,string scheme,InputDevice device)
    {
        _playerInputList.Add(input);
        OnEnableUiPlayerInputAction?.Invoke(input);

        if (string.IsNullOrEmpty(scheme) || device == null) return;

        input.enabled = true;

        input.SwitchCurrentControlScheme(scheme, device);
        input.DeactivateInput();
    }


    private void PlayerInfo(PlayerInput playerInput, int id, Transform spawnPos)
    {
        //コンポーネント構造体を取得
        var com = GetComponents(playerInput);

        SetupCommon(playerInput, com, spawnPos);

        //player1,2の情報をセット
        if (id == PlayerIds.Player1)
            SetupPlayer1(playerInput, com);
        else
            SetupPlayer2(playerInput, com);
    }

    //構造体にplayer情報をセット
    private PlayerComponents GetComponents(PlayerInput input)
    {
        return new PlayerComponents
        {
            processController = input.GetComponent<PlayerProcessController>(),
            uiManager = input.GetComponent<UiInputManager>(),
            handler = input.GetComponent<PlayerInputHandler>(),
            state = input.GetComponent<PlayerState>(),
            move = input.GetComponent<PlayerMovement>(),
            collider = input.GetComponent<AttackFrameController>(),
        };
    }
    private void SetupCommon(PlayerInput input, PlayerComponents component, Transform spawnPos)
    {

        input.transform.position = spawnPos.position;
        input.defaultActionMap = "Player";
        //デバイスが登録されていたら入力On
        if (input.devices.Count > 0 && !string.IsNullOrEmpty(input.currentControlScheme))
        {
            input.SwitchCurrentActionMap("Player");
            input.ActivateInput();
            input.actions.Enable();
        }

        component.processController.SetUpAnimation();
    }
    private void SetupPlayer1(PlayerInput input, PlayerComponents component)
    {
        //debag用のスクリプトに移る
        component.processController.SetDebugManager(player1TextManager);

        int playerId = PlayerIds.Player1;

        component.processController.SetUpPlayer("PlayerA", playerId);

        component.state.SetPlayerInfo(PlayerDataManager.Instance.Player1Data.CustomData);
        battleHud.InitializePlayerUI(component.state);

        //ここは外部で誰が敵かをセットしている
        component.collider.SetOpponentAndSubscribeEvent(Player2InputDevice.GetComponent<AttackFrameController>());
        component.move.SetEnemy(Player2InputDevice.transform);

        cameraFollowManager.SetTarget(input.transform, playerId);
        PlayerImage1.color = new Color(1f, 1f, 1f, 1f);
        PlayerImage1.sprite = PlayerDataManager.Instance.Player1Data.CustomData.Sprite;

        //一回解除してから登録する
        component.processController.UnsubscribeAttackEvent(input, component.handler,component.uiManager);

        component.processController.SubscribeAttackEvent(component.handler, component.uiManager, input);
      
    }
    private void SetupPlayer2(PlayerInput input, PlayerComponents component)
    {
        int playerId = PlayerIds.Player2;
        component.processController.SetDebugManager(player2TextManager);

        component.processController.SetUpPlayer("PlayerB", playerId);

        component.state.SetPlayerInfo(PlayerDataManager.Instance.Player2Data.CustomData);
        battleHud.InitializePlayerUI(component.state);

        //ここは外部で誰が敵かをセットしている
        component.collider.SetOpponentAndSubscribeEvent(Player1InputDevice.GetComponent<AttackFrameController>());
        component.move.SetEnemy(Player1InputDevice.transform);

        cameraFollowManager.SetTarget(input.transform, playerId);
        PlayerImage2.color = new Color(1f, 1f, 1f, 1f);
        PlayerImage2.sprite = PlayerDataManager.Instance.Player2Data.CustomData.Sprite;

        //重複を防ぐため一回解除してから登録する
        component.processController.UnsubscribeAttackEvent(input, component.handler, component.uiManager);

        component.processController.SubscribeAttackEvent(component.handler, component.uiManager, input);

    }
 
    public void StartSet()
    {
        if (Player1InputDevice == null || Player2InputDevice == null)
        {
            Debug.LogError("プレイヤーが初期化されていません！Initilize()の完了前にStartSetが呼ばれています。");
            return; // Null参照を防ぐ
        }

        //生成したキャラクターに情報をセット
        PlayerInfo(Player1InputDevice, PlayerDataManager.Instance.Player1Data.PlayerId, playerPos1);
        PlayerInfo(Player2InputDevice, PlayerDataManager.Instance.Player2Data.PlayerId, playerPos2);

    }
    public void UserRemove()
    {
        for (int i = 0; i < _inputUsersList.Count; i++)
        {
            var user = _inputUsersList[i];
            if (user.valid)
            {
                user.UnpairDevicesAndRemoveUser();
            }
        }
        _inputDevicesList.Clear();
        _inputUsersList.Clear();
    }

    //プレイヤーを取得
    public List<PlayerInput> GetPlayersList() { return _playerInputList; }
    public void ClearList()
    {
        _playerInputList.Clear();
    }
    private void ClearPlayerInputList()
    {

        foreach(var player in _playerInputList)
        {
            if (player.enabled)
                player.DeactivateInput();
        }
    }
    public void EnablePlayerInputList()
    {
        foreach (var player in _playerInputList)
        {
            if (player.enabled)
                player.ActivateInput();
        }
    }
    private void RemoveUser()//入力デバイスの解除
    {
        var alldevice = InputUser.all.ToList();
        foreach (var device in alldevice)
        {
            if (device.valid)
            {
                device.UnpairDevicesAndRemoveUser();
            }
        }
    }
    public void RemovePlayerPos()//バグで落ちたときに移動用
    {
        Vector3 pos1 = playerPos1.position;
        Vector3 pos2 = playerPos2.position;
        Player1InputDevice.transform.position = pos1;
        Player2InputDevice.transform.position = pos2;
    }
    //コンポーネントの構造体
    class PlayerComponents
    {
        public PlayerProcessController processController;
        public UiInputManager uiManager;
        public PlayerInputHandler handler;
        public PlayerState state;
        public PlayerMovement move;
        public AttackFrameController collider;
    }
}