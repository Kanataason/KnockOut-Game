using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public class SwitchActionMap : MonoBehaviour
{
    public void SwitchActionUiMap(PlayerInput playerinput)
    {
        playerinput.SwitchCurrentActionMap("Ui");//UIのActionMapに切り替え
    }

    public void SwitchActionPlayerMap(PlayerInput playerinput)
    {
        playerinput.SwitchCurrentActionMap("Player");//playerのActionMapに切り替え
    }
    public void DisableActionPlayerMap(PlayerInput playerinput)
    {
        if (playerinput == null || playerinput.gameObject == null) return;
        playerinput.DeactivateInput();//プレイヤーの入力を完全に無効
    }
    public void ActiveActionPlayerMap(PlayerInput playerinput)
    {
        playerinput.ActivateInput();//プレイヤーの入力可能
    }
    public void PlayerInputUiModuleDisable(PlayerInput playerinput)//UIの入力を切る
    {
        if (playerinput == null) return;
        playerinput.uiInputModule.enabled = false;//Uiの入力コンポーネントを非表示
    }
    public void PlayerInputUiModuleEnable(PlayerInput playerinput)//UIの入力をつける
    {
        EventSystem.current.GetComponent<InputSystemUIInputModule>().actionsAsset = playerinput.actions;
         playerinput.uiInputModule.enabled = true;//入力のコンポーネントをアクティブにする
    }
    public void EnableActionMap(string Map,InputActionAsset inputActions)//ActionMapを有効
    {
        var map = inputActions.FindActionMap(Map);//引数で渡された名前のアクションマップを捜して取得
        if (map != null)
        {
            map.Enable();//アクションマップを有効にする
        }
        else
        {
            Debug.LogWarning($"Action map '{Map}' not found.");
        }
    }
    public void DisableActionMap(string Map, InputActionAsset inputActions)//ActionMapを無効
    {
        var map = inputActions.FindActionMap(Map);//引数で渡された名前のアクションマップを捜して取得
        if (map != null)
        {
            map.Disable();//アクションマップを無効にする
        }
        else
        {
            Debug.LogWarning($"Action map '{Map}' not found.");
        }
    }
    public void ActivePlayerInput(PlayerInput input,bool IsActive)//入力のOn Off
    {
        input.enabled = IsActive;
    }
    public void EnableOrDisablePlayerInput(PlayerInput input, bool IsEnable = true)//どのデバイスを入力を不可にするかを確認
    {
      
        if (IsEnable)
            input.ActivateInput();//プレイヤーが入力できるようにする
        else
            input.DeactivateInput();//プレイヤーが入力できないようにする

    }
}
