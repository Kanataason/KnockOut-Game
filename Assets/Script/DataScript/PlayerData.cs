using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "Playerinfo",menuName = "PlayerId")]
public class PlayerData : ScriptableObject
{
    public InputDevice CurrentDevice;
    public string CurrentControlScheme;
    public int PlayerId;
    [FormerlySerializedAs("CostomData")] public CustomCharacterData CustomData;
    public GameObject CurrentCharacter;

    //初期化処理
    public void Init()
    {
        CurrentControlScheme = "";
        PlayerId = PlayerIds.None;
        CustomData = null;
        CurrentCharacter = null;
    }
    public void CharacterReset()
    {
        CurrentCharacter = null;
        CustomData = null;
    }
}
