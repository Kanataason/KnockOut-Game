using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerProcessController : MonoBehaviour
{

    private AnimationManager animationController;
    private PlayerController playerController;
    private Animator animator;
    private PlayerState playerState;
    private PlayableAnimationPlayer playablePlayer;

    private PlayerInputHandler playerInputHandler;
    private UiInputManager uiInputManager;

    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private AvatarMask normalMask;
    [SerializeField] private AvatarMask crouchMask;

    private Dictionary<string, Action<InputAction.CallbackContext>> uiActions;
    private Dictionary<string, Action<InputAction.CallbackContext>> normalActions;

    private void InitList()
    {
        uiActions = new()
        {
            {"Cancel",uiInputManager.OnCancel },
            {"Navigate",uiInputManager.OnNavigate },
            {"Submit",uiInputManager.OnSubmit }
        };

        normalActions = new()
        {
            {"Move",playerInputHandler.OnMove},
            {"Jump",playerInputHandler.OnJump },
            {"Crouch",playerInputHandler.OnCrouch},
            {"WeakAttack",playerInputHandler.OnWeakAttack},
            {"MidlleAttack",playerInputHandler.OnMiddleAttack },
            {"StrongAttack",playerInputHandler.OnStrongAttack },
            {"SpecialAttack",playerInputHandler.OnSpecialAttack },
            {"Grap",playerInputHandler.OnThrow },
            {"TiltAttack",playerInputHandler.OnTiltAttack },
            {"SmashAttack",playerInputHandler.OnSmashAttack },

        };
    }
    private void OnDisable()
    {
        UnSubscribeEvents();
    }
    private void UnSubscribeEvents()//イベント解除
    {
        //SetUpAnimationの前に破棄されるとplayerControllerがnullなので確認
        if (PlayerInitializeManager.Instance != null && playerController != null)
            playerController.OnRemovePos -= PlayerInitializeManager.Instance.RemovePlayerPos;
    }
    private void SubscribeEvents()//イベント登録
    {
        playerController.OnRemovePos += PlayerInitializeManager.Instance.RemovePlayerPos;
    }
    public void SetUpPlayer(string tagName,int playerId)//プレイヤーのセットアップをする関数
    {
        SubscribeEvents();

        playerState.PlayerId = playerId;

        gameObject.tag = tagName;
    }
    public void SetUpAnimation()//アニメーション関連の処理セットアップする　１番最初に流れる
    {
        TryGetComponents();

        InitList();

        animator.runtimeAnimatorController = animatorController;

        //参照を渡してあげる アニメーション側がタイミングが違うのでGetComponentできないため
        animationController.SetReferencePlayable(playablePlayer);
    }
    private void TryGetComponents()//コンポーネントを取得
    {
        playerInputHandler = GetComponent<PlayerInputHandler>();
        animationController = GetComponent<AnimationManager>();
        playerController = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
        playerState = GetComponent<PlayerState>();
        playablePlayer = GetComponent<PlayableAnimationPlayer>();
        uiInputManager = GetComponent<UiInputManager>();
    }

    public void SetDebugManager(PlayerDebugTextManager manager)//debag用のテキストを参照をコントローラーに渡す処理
    {
        playerController.SetDebugTextManager(manager);
    }

    //ここからはキー入力を登録・解除する関数
    public void UnsubscribeAttackEvent(PlayerInput input, PlayerInputHandler handler, UiInputManager uiInput)
    {
        //SetUpAnimationの前に呼ばれるとリストがnullなので返す
        if (normalActions == null || uiActions == null) return;
        if (normalActions.Count == 0 || uiActions.Count == 0) return;
        foreach (var action in normalActions)
        {
            var actionName = action.Key;

            input.actions[actionName].performed -= action.Value;
        }
        foreach (var action in normalActions)
        {
            var actionName = action.Key;

            input.actions[actionName].canceled -= action.Value;
        }

        //SubscribeAttackEventで登録したUiの入力も解除して二重登録を防ぐ
        UnsubscribeUiEvent(input, uiInput);
    }

    public void SubscribeAttackEvent(PlayerInputHandler handler, UiInputManager uiManager, PlayerInput input)//入力を受け取る関数と紐づけ
    {
        if (normalActions == null || uiActions == null) return;
        if (normalActions.Count == 0 || uiActions.Count == 0) return;

        foreach (var action in normalActions)
        {
            var actionName = action.Key;

            input.actions[actionName].performed += action.Value;
        }

        foreach (var action in normalActions)
        {
            var actionName = action.Key;

            input.actions[actionName].canceled += action.Value;
        }

        var uiMap = input.actions.FindActionMap("Ui", true);
        uiMap.Enable();

        foreach (var action in uiActions)
        {
            var actionName = action.Key;

            uiMap[actionName].performed += action.Value;
        }
        foreach (var action in uiActions)
        {
            var actionName = action.Key;

            uiMap[actionName].canceled += action.Value;
        }

    }
    public void UnsubscribeUiEvent(PlayerInput input, UiInputManager handler)
    {
        if (uiActions == null || uiActions.Count == 0) return;
        var uiMap = input.actions.FindActionMap("Ui", true);

        foreach (var action in uiActions)
        {
            var actionName = action.Key;

            uiMap[actionName].performed -= action.Value;
        }
        foreach (var action in uiActions)
        {
            var actionName = action.Key;

            uiMap[actionName].canceled -= action.Value;
        }
    }
}
