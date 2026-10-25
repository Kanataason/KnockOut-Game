using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager Instance { get; private set; }

    //シーンをまたいで保持したいからシングルトン
    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public PlayerData Player1Data;//プレイヤー１のデータ
    public PlayerData Player2Data;//プレイヤー２のデータ

    //引数で渡された値を設定する
    private void OnDestroy()
    {
        ClearPlayerData();
    }
    /// <summary>
    /// 渡された情報をPlayerDataに設定していく
    /// </summary>
    public void SetPlayerInfo(int Id, string controlScheme, InputDevice device)
    {
        // 0:Player1 1:Player2
        var data = GetPlayerData(Id);

        data.PlayerId = Id;
        data.CurrentDevice = device;
        data.CurrentControlScheme = controlScheme;
    }
    /// <summary>
    /// セレクト画面で選択したキャラクターを設定
    /// </summary>
    public void SetPlayerCharacter(int Id, CustomCharacterData customData, GameObject character)
    {
        // 0:Player1 1:Player2 (SetPlayerInfoと同じ番号)
        var target = GetPlayerData(Id);
        target.CustomData = customData;
        target.CurrentCharacter = character;
    }
    /// <summary>
    /// 設定前やゲーム終了時に呼ばれる
    /// </summary>
    public void ClearPlayerData()
    {
        //登録されている情報をすべて初期化
        Player1Data.Init();
        Player2Data.Init();
    }
    /// <summary>
    /// セレクト画面時にキャンセルしたら呼ばれる
    /// </summary>
    public void CharacterReset()
    {
        //キャラクターに関することだけを初期化
        Player1Data.CharacterReset();
        Player2Data.CharacterReset();
    }
    /// <summary>
    /// Idにあったdataを取得して返す
    /// </summary>
    public PlayerData GetPlayerData(int Id)
    {

        if (Id == PlayerIds.Player1) return Player1Data;
        else return Player2Data;
    }
}
