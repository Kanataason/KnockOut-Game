using UnityEngine;
using UnityEngine.InputSystem;

public class SetSelectInputUser : MonoBehaviour
{

    /// <summary>
    /// PlayerInputの接続を自動じゃなくしてUserを新しく紐づける
    /// </summary>

    private string _currentScheme = "";
    private InputDevice _currentDevice = null;

    /// <summary>
    /// Userの新しい登録
    /// </summary>
    public void SetInputUser(PlayerInput input,int PlayerId)
    {
        input.neverAutoSwitchControlSchemes = true;
        var data = PlayerDataManager.Instance.GetPlayerData(PlayerId);

        _currentScheme = data.CurrentControlScheme;
        _currentDevice = data.CurrentDevice;

        CheckNull();
 
        input.enabled = true;
        input.SwitchCurrentControlScheme(_currentScheme, _currentDevice);
        input.ActivateInput();
    }

    /// <summary>
    /// Nullがあるかをチェックして開ければ新しく設定
    /// </summary>
    private void CheckNull()
    {
        if (string.IsNullOrEmpty(_currentScheme) || _currentDevice == null)
        {
            bool IsPlayGamePad = Gamepad.all.Count > 0;

            Debug.LogError("ControlScheme or Device null");

            _currentScheme = IsPlayGamePad ? "GamePad" : "KeyBoard";
            _currentDevice = IsPlayGamePad ? Gamepad.all[0] : Keyboard.current;
        }
    }
    
}
