using UnityEngine;

public class CharacterSelectionRecorder : MonoBehaviour
{
    private UiCharacterSelectMenu selectManager;
    private void Start()
    {
        selectManager = GetComponent<UiCharacterSelectMenu>();
        RegisterEvents();
    }
    private void RegisterEvents()//イベントを登録
    {
        if (selectManager != null)
            selectManager.OnSetCustomCharacterAction += OnSelectedChecker;
    }
    private void OnDisable()//イベントを解除
    {
        if (selectManager != null)
            selectManager.OnSetCustomCharacterAction -= OnSelectedChecker;
    }
    public void OnSelectedChecker(int Id,CustomCharacterData data,GameObject character)//キャラクター選択時に流れる
    {
        //選択したキャラクターをプレイヤーデータにセット
        PlayerDataManager.Instance.SetPlayerCharacter(Id, data,character);
    }
}
