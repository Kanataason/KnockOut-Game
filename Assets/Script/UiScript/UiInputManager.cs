using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class UiInputManager : MonoBehaviour
{
    /*---------------Ui用-------------*/

    public Vector2 Navigate { get; private set; }

    public event Action OnCancelAction;//プレイヤーがキャンセルボタンを押したときに通知するためのアクション
    public event Action OnSubmitAction;//プレイヤーが決定ボタンを押したときに通知するためのアクション
    /*---------------Ui用-------------*/
    public static GameObject CurrentSelectedObject;//現在選択しているボタン
    private void OnDestroy()
    {
        CurrentSelectedObject = null;
    }
    private void OnEnable()
    {
        var playerInput = GetComponent<PlayerInput>();
        //入力するコンポーネントが存在しいていないか、有効じゃ無ければ返す
        if (playerInput == null||!playerInput.enabled) return;
        //プレイヤーの操作を有効にする
        if (playerInput.inputIsActive)
            playerInput.actions.Enable();

    }
    private void OnDisable()
    {
        var playerInput = GetComponent<PlayerInput>();

        if (playerInput == null||!playerInput.enabled) return;

        //プレイヤーの操作を切る
        if (playerInput.inputIsActive)
        playerInput.actions.Disable();
    }
    private void Start()//現在UIで選択しているボタンをセット
    {
         CurrentSelectedObject = EventSystem.current.currentSelectedGameObject;
    }
 


    /*------------------------------------ここからUiの値取得-----------------------------------------*/
    public void OnNavigate(InputAction.CallbackContext ctx)
    {
      
        if (ctx.started)
        {
            Navigate = ctx.ReadValue<Vector2>();
        }
        SelectButton();

        if (ctx.canceled)
        {
            Navigate = Vector2.zero;//入力を初期化
        }
    }
    public void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            OnSubmitAction?.Invoke();
        }

    }
    public void OnCancel(InputAction.CallbackContext ctx)
    {
        if (ctx.started)
        {
            OnCancelAction?.Invoke();
        }

    }
    private void SelectButton()//音を鳴らすだけ
    {
        if (EventSystem.current == null) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;

        //現在選んでいるオブジェクトが前選んでいたオブジェクトじゃなければ流す
        if (selected != null && selected != CurrentSelectedObject)
        {
            CurrentSelectedObject = selected;
            AudioManager.Instance.PlaySe(AudioManager.SeState.UiMove);//Seを流す
        }
    }
}

