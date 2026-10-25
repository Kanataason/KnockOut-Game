using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    //押したときのステート
    public enum InputState
    {
        Performed,
        Canceled
    }
    //アクションの種類
    public enum ActionState
    {
        WeakAttack,
        MiddleAttack,
        StrongAttack,
        SpecialAttack,
        TiltAttack,
        SmashAttack,
        Jumping,
        Crouching,
        Grabbing,
        None,
        CancelAction,
    }
    public Vector2 MoveInput { get; private set; }//移動値
    public bool IsCrouching { get; private set; }//しゃがんでいる状態

    //何が押されたか情報を渡すアクション
    public System.Action<InputState, ActionState> OnAction;

    /// <summary>
    /// 小攻撃コールバック
    /// </summary>
    public void OnWeakAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnAction?.Invoke(InputState.Performed, ActionState.WeakAttack);
        }
        else if (context.canceled)
        {
            OnAction?.Invoke(InputState.Canceled, ActionState.WeakAttack);
        }
    }
    /// <summary>
    /// 中攻撃コールバック
    /// </summary>
    public void OnMiddleAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnAction?.Invoke(InputState.Performed, ActionState.MiddleAttack);
        }
        else if (context.canceled)
        {
            OnAction?.Invoke(InputState.Canceled, ActionState.MiddleAttack);
        }
    }
    /// <summary>
    /// 大攻撃コールバック
    /// </summary>
    public void OnStrongAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnAction?.Invoke(InputState.Performed, ActionState.StrongAttack);
        }
        else if (context.canceled)
        {
            OnAction?.Invoke(InputState.Canceled, ActionState.StrongAttack);
        }
    }

    /// <summary>
    /// 特殊な攻撃コールバック
    /// </summary>
    public void OnSpecialAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            OnAction?.Invoke(InputState.Performed, ActionState.SpecialAttack);
        }
        else if (context.canceled)
        {
            OnAction?.Invoke(InputState.Canceled, ActionState.SpecialAttack);
        }
    }
    /// <summary>
    /// つかみのコールバック
    /// </summary>
    public void OnThrow(InputAction.CallbackContext ctx)
    {
        if(ctx.performed)
        {
            OnAction?.Invoke(InputState.Performed, ActionState.Grabbing);
        }
        else if (ctx.canceled)
        {
            OnAction?.Invoke(InputState.Canceled, ActionState.Grabbing);
        }
    }
    /// <summary>
    /// 移動系の攻撃コールバック
    /// </summary>
    public void OnTiltAttack(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            OnAction?.Invoke(InputState.Performed, ActionState.TiltAttack);
        }
        else if (ctx.canceled)
        {
            OnAction?.Invoke(InputState.Canceled, ActionState.TiltAttack);
        }
    }
    /// <summary>
    /// 必殺技のコールバック
    /// </summary>
    public void OnSmashAttack(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
        {
            OnAction?.Invoke(InputState.Performed, ActionState.SmashAttack);
        }
        else if (ctx.canceled)
        {
            OnAction?.Invoke(InputState.Canceled, ActionState.SmashAttack);
        }
    }
    /// <summary>
    /// 技をキャンセルするコールバック
    /// </summary>
    public void OnCancelAction(InputAction.CallbackContext ctx)
    {
    }


    /// <summary>
    /// ここからはノーマルアニメーション用のCallBack
    /// </summary>


    public void OnMove(InputAction.CallbackContext context)
    {

        Vector2 input = context.ReadValue<Vector2>();
        MoveInput = input;


        if (context.canceled)
        {
            MoveInput = Vector2.zero;
        }
    }
    /// <summary>
    /// ジャンプのコールバック
    /// </summary>
    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            OnAction?.Invoke(InputState.Canceled, ActionState.Jumping);
        }
        else if (context.performed)
        {
            OnAction?.Invoke(InputState.Performed, ActionState.Jumping);
        }
    }
    /// <summary>
    /// しゃがみのコールバック
    /// </summary>
    public void OnCrouch(InputAction.CallbackContext ctx)
    {

        if (ctx.canceled)
        {
            OnAction?.Invoke(InputState.Canceled, ActionState.Crouching);
            IsCrouching = false;
        }
        else if (ctx.performed)
        {
            IsCrouching = true;
            OnAction?.Invoke(InputState.Performed, ActionState.Crouching);
        }
    }

}