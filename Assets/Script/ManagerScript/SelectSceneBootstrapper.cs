using UnityEngine;

public class SelectSceneBootstrapper : MonoBehaviour
{
    [SerializeField] private UiCharacterSelectMenu _selectManager;
    [SerializeField] private SetSelectInputUser _selectUser;
    [SerializeField] private GameObject _mainManager;
    void Start()
    {
        CreateManager();
    }
    private void CreateManager()//オブジェクトを生成して情報を渡す
    {
        var manager = Instantiate(_mainManager).GetComponent<MainManager>();
        manager.SubscribeEvents(_selectManager);
        _selectManager.Init(_selectUser);
    }

}
