using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Users;
using UnityEngine.UI;
using static UiSelectInfoTextManager;
public class UiCharacterSelectMenu : MonoBehaviour
{

    [System.Serializable]
    public class PlayerInfo//playerの情報を入力する
    {
        //プレイヤーごとに持っている物
        public PlayerInput PlayerInput;//プレイヤーの操作コンポーネント
        public Transform ModelSpawnPoint;//スポーン場所
        public bool IsSelected = false;//選択済みかを確認するフラグ
        //現在選んでいるもの
         public GameObject CurrentModel;//現在表示しているキャラクター
        [HideInInspector]public GameObject CurrentButton;//現在選択しているボタン
        [HideInInspector]public CharacterEffectType CurrentType;//現在選択しているキャラクターの種類ステータス
    }

    [Header("キャラデータ")]
    public CustomCharacterData[] CharacterData;//キャラクターの情報

    [Header("プレイヤー管理")]
    public PlayerInfo[] PlayersList; // プレイヤー1・2の情報を格納
    public PlayerInput Player1Input;
    public PlayerInput Player2Input;

    private InputSystemUIInputModule currentUiInputModule;

    private int _currentPlayerIndex = 0;//現在の選択index
    private bool isLoaded = false;
    private bool isCover = false;

    //アクションイベント

    //操作デバイスが変わったら流れるアクション
    public event Action<PlayerInput> OnInputAction;

    //プレイヤーがキャンセルボタンを押したら流れるアクション
    public event Action<UiCharacterSelectMenu> OnCancelInputAction;

    //プレイヤーが決定ボタンを押したら流れるアクション
    public event Action<int, CustomCharacterData,GameObject> OnSetCustomCharacterAction;

    //参照
    private SetSelectInputUser _inputUser;
    private UiSelectInfoTextManager _textManager;
    private GameObject _currentSelectedUI;

    //オブジェクトプール
    [FormerlySerializedAs("addresableObjectPool")] [SerializeField] AddressableObjectPool<CharacterEffectType> addressableObjectPool = new();
    private void Awake()
    {
        InitPlayerInput();
    }
    private void InitPlayerInput()//入力の順序を設定
    {
        FindUiInputModuleAndActive();//UI操作をOffにする

        PlayersList[0].PlayerInput = Player1Input;
        PlayersList[1].PlayerInput = Player2Input;

    }
    private void FindUiInputModuleAndActive(bool isActive = false)
    {
        if (currentUiInputModule != null)
        {
            currentUiInputModule.enabled = isActive;
            return;
        }

        var eventSystem = EventSystem.current;

        if (eventSystem.TryGetComponent<InputSystemUIInputModule>(out currentUiInputModule))
        {
            currentUiInputModule.enabled = isActive;
        }
    }

    private void OnEnable()//イベントを登録
    {
        if (EventManager.Instance != null)
            EventManager.Instance.OnSelectCancelAction += CancelSelection;
    }
    private void OnDisable()//イベントの解除
    {
        OnCancelInputAction?.Invoke(this);
        addressableObjectPool.Release();

        if(EventManager.Instance != null)
        EventManager.Instance.OnSelectCancelAction -= CancelSelection;
    }
    public void Init(SetSelectInputUser inputUser)//最初のplayerだけinputを可能にする
    {
        _inputUser = inputUser;

        //操作デバイスを変える通知を実行
        OnInputAction?.Invoke(PlayersList[_currentPlayerIndex].PlayerInput);
    }

    private async void Start()
    {

        InputUser.listenForUnpairedDeviceActivity = 0;

        await addressableObjectPool.Init();

        isLoaded = true;//ロードが完了したことを知らせる

        RegisterEvents();
        FindUiInputModuleAndActive(true);//UI操作をOnにする

        UpdateCurrentSelectCharaText(_currentPlayerIndex);

        UpdateInputState();
    }
    private void RegisterEvents()//コンポーネントを取得
    {
        _textManager = GetComponent<UiSelectInfoTextManager>();
    }

    private void Update()
    {
        if (!isLoaded || !IsMyTurn()) return;

        CheckSelectButton();//選んでいるボタンの確認
    }
    private bool IsMyTurn()//現在入力したplayerが任意の数超えていてかつ自分が選択してなかったら
    {
        return _currentPlayerIndex < PlayersList.Length && PlayersList[_currentPlayerIndex].IsSelected == false;
    }

    private void CheckSelectButton()//現在選んでいるボタンの確認
    {
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        //現在選んでいる物が前選んだものと違っていたら流す
        if (selected != null && selected != _currentSelectedUI)
        {
            _currentSelectedUI = selected;
            var buttonData = selected.GetComponent<SelectedCharacterButtonData>();
            if (buttonData != null)
            {
                //キャラクター表示処理
                SelectCharacter(buttonData.CharacterData.ButtonNumber);
            }
        }
    }

    private void UpdateInputState()//入力の順番を決める
    {
        UpdateCurrentSelectCharaText(_currentPlayerIndex);//テキストに現在選んでいるplayerを反映

        for (int i = 0; i < PlayersList.Length; i++)
        {
            if(i != _currentPlayerIndex)//自分の操作ターンじゃなかったら操作を無効にする
            {
                EventManager.Instance.EnableOrDisablePlayerInput(PlayersList[i].PlayerInput, false);
                EventManager.Instance.ChangeActivePlayerInput(PlayersList[i].PlayerInput);
                continue;
            }
            //自分の操作ターンなら操作を有効にする
            _inputUser.SetInputUser(PlayersList[i].PlayerInput,_currentPlayerIndex);
            EventManager.Instance.UiModuleEnable(PlayersList[i].PlayerInput);
        }
        UpdateSelectButton();//最初に選択しているボタンを設定
    }
    private void UpdateSelectButton()//選ぶボタンを更新
    {
        GameObject prevButton = PlayersList[_currentPlayerIndex].CurrentButton;
        //過去に選択していたボタンがあるかを確認
        if (prevButton != null)
        {
            prevButton.GetComponent<Button>().Select();
        }
        else
        {
            EventManager.Instance.ResetFirstSelectButton();
        }
        //現在選択されているUIボタンに対応するキャラを表示
        CheckSelectButton();
    }
    private void UpdateCurrentSelectCharaText(int CharaId)//テキストに反映させる
    {;//０から始まるから
        _textManager.SetText(CharaId,SelectTextState.Selecting,
            $"CHARACTER SELECT{CharaId+1}");
    }

    public void ConfirmSelection(CustomCharacterData customData)//ボタンを押したら呼ばれる
    {
        if (_currentPlayerIndex >= PlayersList.Length) return;//playerの人数を超えていたら終わる

        var player = PlayersList[_currentPlayerIndex];

        player.IsSelected = true;//選択済みにする
        player.CurrentButton = _currentSelectedUI;//現在選んでいるボタンを設定
        _currentSelectedUI = null;

        //選んだキャラクターの情報をセットさせる溜めの通知を実行
        //PlayerDataManagerと番号を合わせるため0始まりのindexを渡す（0:Player1 1:Player2）
        OnSetCustomCharacterAction?.Invoke(_currentPlayerIndex, customData, addressableObjectPool.GetPrefab(player.CurrentType));
        CheckSelectedPlayer();//選択済みのキャラクター確認
    }
    private void CheckSelectedPlayer()
    {
        _currentPlayerIndex++;
        //全員選択完了していたら流す
        if (_currentPlayerIndex >= PlayersList.Length)
        {
            EventManager.Instance.BattleStart();
        }
        else
        {
            OnInputAction?.Invoke(PlayersList[_currentPlayerIndex].PlayerInput);//入力の変更を通知
            UpdateInputState();//入力の変更
        }
    }

    public void CancelSelection()//キャンセルボタンを押したらなる
    {
        //最初に選択していたキャラクターがキャンセルボタンを押したら流れる
        if (_currentPlayerIndex <= PlayerIds.Player1)
        {
            //シーンを遷移してUiのすてーたすを変更する
            GameManager.Instance.LoadNextStage(StageInfo.GameMenu, UiState.Home);
            PlayerDataManager.Instance.ClearPlayerData();//すべてのデータを初期化
        }
        else if (_currentPlayerIndex == PlayerIds.Player2)
        {
            InitPlayerInfo();//テキストやデータの初期化処理

            OnInputAction?.Invoke(PlayersList[_currentPlayerIndex].PlayerInput);//入力の変更を通知
            UpdateInputState();//入力の変更
        }
    }
    private void InitPlayerInfo()//キャンセル時の初期化処理
    {
        var player = PlayersList[_currentPlayerIndex];
        if (player == null) return;

        //テキストの初期化処理
        _textManager.SetText(_currentPlayerIndex, SelectTextState.Name,
           "");

        _textManager.SetText(_currentPlayerIndex, SelectTextState.Explanation,
             "");
        PlayerDataManager.Instance.CharacterReset();//キャラクターの情報だけ初期化

        if(player.CurrentModel != null)
         addressableObjectPool.Return(player.CurrentType, player.CurrentModel);

        player.CurrentModel = null;
        isCover = false;

        _currentPlayerIndex = 0;
        foreach (var p in PlayersList)
        {
            p.IsSelected = false;
        }
    }
    public void SelectCharacter(int index)
    {
        //リストチェック
        if (!CheckNormality(index)) return;

        CustomCharacterData customData = CharacterData[index];

        var player = PlayersList[_currentPlayerIndex];

        CheckPlayerOverlap(customData);//被りチェック

        ShowCharacter(player, customData);//キャラクターを表示

    }
    private void CheckPlayerOverlap(CustomCharacterData customData)//キャラクターが被っているかを確認
    {

        var playerData = PlayerDataManager.Instance != null
            ? PlayerDataManager.Instance.GetPlayerData(PlayerIds.Player1)
            : null;
        if (playerData == null || customData == null) return;

        //被っているか確認
        if (playerData.CustomData != null)
        {
            isCover = (customData.CharacterModel == playerData.CustomData.CharacterModel);
        }
    }
    private bool CheckNormality(int index)//正常かどうかをチェック
    {
        // リストの範囲に収まっているかを確認
        if (index < 0 || index >= CharacterData.Length)
        {
            Debug.LogError($"CharacterData[{index}] が無効です");
            return false;
        }

        return true;
    }

    //キャラクターを表示させる処理
    private void ShowCharacter(PlayerInfo player,CustomCharacterData customData)
    {
        //ObjectPoolに返却

        if (player.CurrentModel != null)
            addressableObjectPool.Return(player.CurrentType, player.CurrentModel);

        SetTexts(customData);

        if (customData.CharacterModel != null)
        {
            //player1が選んだキャラクターと被っているか確認
            var type = isCover
       ? customData.CharacterType2P
       : customData.CharacterType;

            var obj = addressableObjectPool.Get(type);

            obj.transform.position = player.ModelSpawnPoint.position;
            obj.transform.rotation = Quaternion.identity;

            player.CurrentType = type;
            player.CurrentModel = obj;  
        }
    }
    private void SetTexts(CustomCharacterData customData)
    {
        //テキストに反映
        _textManager.SetText(_currentPlayerIndex, SelectTextState.Name,
            customData.CharacterName);

        _textManager.SetText(_currentPlayerIndex, SelectTextState.Hp,
             $"{SelectTextState.Hp.ToString()} : {customData.Hp}");

        _textManager.SetText(_currentPlayerIndex, SelectTextState.Type,
            $"{SelectTextState.Type.ToString()}: {customData.AttributeType}");

        _textManager.SetText(_currentPlayerIndex, SelectTextState.Difficulty,
            $"{SelectTextState.Difficulty.ToString()} : {customData.Difficulty}");
    }
}
