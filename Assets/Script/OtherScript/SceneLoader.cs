using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class SceneLoader : MonoBehaviour
{
    TitleManager _titleManager;
    public Slider ProgressBar;          // (任意) ロード進捗を表示するスライダー

    Action<string> OnChangeScene;
    public void Init(TitleManager tmanager,Action<string> changescene)//生成されて呼ばれる
    {
        _titleManager = tmanager;//渡された参照を保存
        OnChangeScene = changescene;//渡されたアクションを保存

        SubscribeEvents();
    }
    private void SubscribeEvents()//イベント登録
    {
        if (_titleManager != null)
            _titleManager.OnTitleInput += OnLoadSceneStart;
    }

    private void OnDisable()//イベント解除
    {
        if (_titleManager == null) return;

        _titleManager.OnTitleInput -= OnLoadSceneStart;
    }
    private void OnLoadSceneStart()
    {
        //入力されたら流れる
        StartCoroutine(LoadNextScene());
    }
    IEnumerator LoadNextScene()//ロードの進行度を確認
    {
        yield return new WaitForSeconds(3);

        //ゲームメニューのシーンにあるオブジェクトを読み込む
        AsyncOperation async = SceneManager.LoadSceneAsync(StageInfo.GameMenu);
        async.allowSceneActivation = false;

        while (!async.isDone)//読み込みが終わったら処理
        {
            //0〜１の範囲で値を保持
            float progress = Mathf.Clamp01(async.progress / 0.9f);

            //スクロールバーが存在していたら値をセット
            if (ProgressBar != null)
                ProgressBar.value = progress;

            //９割終わったらシーン遷移準備
            if (async.progress >= 0.9f)
            {
                async.allowSceneActivation = true;
                string stageName = StageInfo.GameMenu;
                OnChangeScene?.Invoke(stageName);//シーン遷移
                break;
            }

            yield return null;
        }
    }
}

[Serializable]
public static class StageInfo//各シーンの名前をここで宣言
{
    public const string GameMenu = "GameMenuScene";
    public const string Staffroll = "StaffrollScene";
    public const string Battle = "BattleScene";
    public const string SelectCharacter = "SelectCharacterScene";
    public const string Title = "TitleScene";
    
}
