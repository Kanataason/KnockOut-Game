using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Video;
using UnityEngine.UI;
using System.Collections;
public class HomeGameManager : MonoBehaviour
{
    /// <summary>
    /// GameMenuのビデオを流したりキャラクターを表示させたりする
    /// </summary>

    [SerializeField] private float idleTime = 30;//入力待機時間
    [SerializeField] private VideoPlayer videoPlayer;//カメラ
    [SerializeField] private RawImage rawImage;//再生用のイメージ
    [SerializeField] private GameObject target;//ビデオを移す溜めのイメージ

    [SerializeField] private bool enableIdleVideo = false;
    //ホームに出すキャラクターをリストに保存
    public List<GameObject> CharactersList = new();

    //------内部で使われる変数
    private bool isVideoStopped = false;
    private float timer = 0f;
    //--------------
    private void OnEnable()
    {
        InitList();
    }
    private void InitList()//ランダムでホームに出すキャラクターを選定
    {
        var random = Random.Range(0, CharactersList.Count);

        GameObject chara = CharactersList[random];
        chara.SetActive(true);
    }
    //--------------
    private void OnDestroy()
    {
        
        //ビデオが出ていたら消す
        if (target != null&&target.activeSelf)
            target.SetActive(false);
        //動画が再生中なら再生を止める
        if (videoPlayer != null&&videoPlayer.isPlaying)
            videoPlayer.Stop();
    }
    //---------------
    void Start()
    {
        //音のリストはAudioManagerのAwakeで準備されるので待たずに初期化する
        Init();
        // 動画が準備できたら表示

        EventEnter();

        // 動画を準備開始
        videoPlayer.Stop();
    }
    private void EventEnter()//ビデオの準備が完了したら流れる関数の登録
    {
        //動画の準備ができたときのイベントを登録
        videoPlayer.prepareCompleted += (vp) =>
        {
            //流す用のイメージに動画をセット
            rawImage.texture = vp.targetTexture;
        };
    }
    private void Init()//初期化
    {
        target.SetActive(false);
        videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        GameManager.Instance.ChangeState(UiState.Home);//ステータスをhomeに変更
        AudioManager.Instance.PlayBGM(AudioManager.BGMState.GameMenu);//home用のBGMに変更

        timer = 0f;
    }
    //------------------

    //------------------
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            enableIdleVideo = !enableIdleVideo;
        }
        //ホーム画面じゃなければ流さない
        if (GameManager.Instance.CurrentUiState != UiState.Home) return;

        if (!enableIdleVideo) return;

        if(Input.anyKeyDown)//何かボタンをおしたら動画を止める
        {
            if (isVideoStopped) return;//すでに再生済みなら流さない

            int disableVideo = 2;
            int timer = 0;
            SetVideoInfo(true, false, timer, disableVideo);//ビデオの状態をセット

            //プレイ中なら止める
            if (videoPlayer.isPlaying)
                videoPlayer.Stop();
        }
        else
        {
            timer += Time.deltaTime;
            //指定した時間入力されなくプレイされてなかったら流す
            if(timer >= idleTime && !videoPlayer.isPlaying)
            {
                int enableVideo = 1;
                int timer = 0;
                SetVideoInfo(false, true, timer, enableVideo);//ビデオの状態をセット

                StartCoroutine(PlayVideo(videoPlayer));
            }
        }
    }
    private void SetVideoInfo(bool IsPlay,bool IsActive, float Timer,int StopSe)//ビデオの状態をセット
    {
        timer = Timer;

        isVideoStopped = IsPlay;
        target.SetActive(IsActive);

        EventManager.Instance.IdleVideoPlayer(StopSe);//フェードさせる
    }
    //----------------------
    IEnumerator PlayVideo(VideoPlayer videoPlayer)//ビデオを流す
    {
        videoPlayer.Prepare();//準備状態にする

        // 準備が終わるまで待つ
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        videoPlayer.Play();//動画を流す
    }
}
