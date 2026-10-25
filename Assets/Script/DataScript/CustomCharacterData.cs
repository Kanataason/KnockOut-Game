using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
[CreateAssetMenu(fileName = "ChractorData",menuName ="ChractorName")]

public class CustomCharacterData : ScriptableObject
{
    public enum PlayerType { Speed, Power, Tank };//キャラクターのタイプステート
    public PlayerType AttributeType;//タイプ

    public CharacterEffectType CharacterType;//キャラタイプ
    public CharacterEffectType CharacterType2P;//2pキャラタイプ
    public int ButtonNumber;//キャラ番号
    public string CharacterName;//キャラ名前
    public float Hp;
    public DifficultyState Difficulty;//難易度
    [FormerlySerializedAs("Chractor")] public GameObject CharacterModel;//3dモデル
  [TextArea] public string CharacterExplanation;//キャラ説明
    public Sprite Sprite;

    public enum DifficultyState //難易度のステート
    {
        Easy,
        Normal,
        Hard,
        Expert
    }

}
