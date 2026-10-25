using UnityEngine;

public class HomeSceneBootstrapper : MonoBehaviour
{

    /// <summary>
    /// マネージャーを生成することでAwakeやStartのタイミングを合わせるためのスクリプト
    /// </summary>

    [SerializeField] private TitleManager titleManager;
    [SerializeField] private GameObject loadSceneManager;
    [SerializeField] private GameManager gameManager;


    private void Start()
    {
        CreateManager();
    }
    private void CreateManager()
    {
        //生成して初期化処理を呼ぶ際に参照も渡してあげる
        var lmanager = Instantiate(loadSceneManager).GetComponent<SceneLoader>();
        lmanager.Init(titleManager,ChangeStage);
    }

    //タイトルのシーンからシーンを遷移
    public void ChangeStage(string name)
    {
        gameManager.LoadNextStage(name,UiState.Home);
    }
}
