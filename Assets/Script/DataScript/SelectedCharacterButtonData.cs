using UnityEngine;
using UnityEngine.Serialization;

public class SelectedCharacterButtonData : MonoBehaviour
{
    /// <summary>
    /// ボタンを押したときにキャラクターのデータを渡すためのスクリプト
    /// </summary>
    [FormerlySerializedAs("ChractorDatas")] public CustomCharacterData CharacterData;
}
