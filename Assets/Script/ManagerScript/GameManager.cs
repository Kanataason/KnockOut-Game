using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Uiの種類分ステータスを宣言
/// </summary>
public enum UiState 
{ 
   Title,
   Home,
   Setting,
   Manual,
   SetPlayer,
   GameStart,
   SelectCharacter,
   MainGame,
   Battle,
   Pause,
   SettingInfo,
   ManualInfo,
   None
}
public class GameManager : MonoBehaviour
{
    //現在のプレイ人数ステート
    public enum PlayMode
    {
        Solo,
        Multi
    }
    [Header("Reference")]
    public static GameManager Instance { get; private set; }


    public UiState CurrentUiState = UiState.Title;
    public PlayMode CurrentMode;//プレイモード

    public Volume Volume;//画面のエフェクト設定用
    public Image FadeImage;//画面の明るさ設定用

    public bool IsSoloKeyboardMode = false;

    //カーソルをロックしているかの変数
    private float moveThreshold = 1f;
    private bool isCursorLocked = false;


    //シーンをまたいで保持したいからシングルトン
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Application.targetFrameRate = 60;//フレームを60に固定
        Cursor.lockState = CursorLockMode.Locked;//カーソルを隠す
    }
    private void Update()
    {
        //プレイヤーの入力を取得
        //マウスが接続されていない環境ではnullになる
        var mouse = Mouse.current;
        Vector2 delta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;

        var anyButton = Input.anyKeyDown;

        if (anyButton && isCursorLocked)
        {
            SetCursorLock();
        }
        // 少しでも動いたら解除 sqrは二乗で返すから値も二乗しないといけない
        if (delta.sqrMagnitude > moveThreshold * moveThreshold && !isCursorLocked)
        {
            SetCursorLock(true);
        }

    }
    //カーソルを表示させる関数
    private void SetCursorLock(bool Active = false)
    {
        CursorLockMode mode = Active ? CursorLockMode.None : CursorLockMode.Locked;

        //カーソルの情報をセット
        Cursor.lockState = mode;
        Cursor.visible = Active;
        isCursorLocked = Active;

    }
    /// <summary>
    /// 指定したシーンへ遷移する
    /// </summary>
    public void ChangeScene(string Name)
    {
        SceneManager.LoadScene(Name);
    }
    /// <summary>
    /// UIの状態を変更する
    /// </summary>
    public void ChangeState(UiState name)
    {
        CurrentUiState = name;
    }
    /// <summary>
    /// プレイモードを変更する
    /// </summary>
    public void ChangePlayMode(PlayMode mode)
    {
        CurrentMode = mode;
    }

    /// <summary>
    /// この変数を有効にすることでキーボードだけでプレイすることができる
    /// </summary>
    public void ChangeSoloKeyBoard()//キーボード単独仕様フラグ
    {
        IsSoloKeyboardMode = !IsSoloKeyboardMode;
        InputUserMakeManager.Instance.ChangeController();//プレイヤーの入力を新しく登録
    }
    public void ExitGame()//ゲーム終了処理する関数
    {
        StartCoroutine(ExitTime());
    }

    public void LoadNextStage(string name,UiState uiState)//タイトルから呼ばれる
    {
        ChangeState(uiState);
        ChangeScene(name);
    }
     IEnumerator ExitTime()//ゲームを終わるための関数
    {
        yield return new WaitForSeconds(1);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }

}
