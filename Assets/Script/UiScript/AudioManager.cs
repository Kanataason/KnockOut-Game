using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
public class AudioManager : MonoBehaviour
{
    /// <summary>
    /// 各種類に合ったステータスを宣言
    /// </summary>
    public enum BGMState
    {
        None = 0,
        Battle,
        Select,
        GameMenu,
        Win,
        LastBoss
    }
    public enum SeState
    {
        None = 0, 
        UiMove,
        Click,
        Return,
        Hit,
        Guard,
        HadakaSA,
        RobotSA
    }
    public enum VoiceState
    {
        None =0,
        KnockOut,
        Win,
        Round1,
        Round2,
        FinalRound,
        Title,
        GameStart,
        Manual,
        Exit,
        Setting
    }

    //シーンをまたいで保持しておきたいからシングルトン
    public static AudioManager Instance { get; private set; }
    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            //他のスクリプトのStartから音を鳴らしても間に合うようにAwakeで準備する
            InitAudioList();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //各音を鳴らすためのオーディオソース
     public AudioSource BGMSource;
     public AudioSource SeSource;
     public AudioSource VoiceSource;

    [SerializeField] private AudioInfo<BGMState> lBGMList;
    [SerializeField] private AudioInfo<SeState> lSeList;
    [SerializeField] private AudioInfo<VoiceState> lVoiceList;

    private void OnDestroy()
    {
        lBGMList.Release();
        lSeList.Release();
        lVoiceList.Release();
    }
    //各リスト初期化
    private void InitAudioList()
    {
        lBGMList.InitList();
        lSeList.InitList();
        lVoiceList.InitList();
    }
    /// <summary>
    /// 他のスクリプトからも音を再生できるようにtypeを渡してclipを鳴らす
    /// </summary>
    public async void PlayBGM(BGMState state)
    {
        StopBGM();

        var clip = await lBGMList.Get(state);

        if (clip == null) return;

        BGMSource.clip = clip;
        BGMSource.Play();
    }
    public async void PlaySe(SeState state)
    {
      //  SeSource.Stop();

        var clip = await lSeList.Get(state);

        if (clip == null) return;

        //再生
        SeSource.PlayOneShot(clip);
    }
    public async void PlayVoice(VoiceState state)
    {
        VoiceSource.Stop();
        var clip =  await lVoiceList.Get(state);

        if (clip == null) return;

        VoiceSource.PlayOneShot(clip);
    }
    public void RandomBGM()
    {
        int rand = UnityEngine.Random.Range(0, 2);

        var bgmState = rand == 0 ?
           BGMState.Battle : BGMState.LastBoss;

        PlayBGM(bgmState);
    }
    public void StopBGM()//Bgmをストップさせる
    {
        BGMSource.Stop();
    }

    public void PlayRoundVoice(int currentRound)//バトル中のラウンドのボイス
    {
        var voiceState = currentRound switch
        {
            1 => VoiceState.Round1,
            2 => VoiceState.Round2,
            3 => VoiceState.FinalRound,
            _ => default,
        };

        PlayVoice(voiceState);
    }
}
[Serializable]
public class AudioInfo<T>
{
    [Serializable]
    public class Entry
    {
        public T e_State;

        // AudioClip → Addressable参照
        public AssetReferenceT<AudioClip> c_AudioClip;
    }

    [SerializeField] private List<Entry> d_List;

    //非同期で再生
    private Dictionary<T, AssetReferenceT<AudioClip>> audioList = new();

    //一度生成したものを保持
    private Dictionary<T, AudioClip> cacheList = new();

    //生成元を保持
    private Dictionary<T, AsyncOperationHandle<AudioClip>> handleList = new();

    //ローディング中のものを保持
    private Dictionary<T, AsyncOperationHandle<AudioClip>> loadingHandles= new();
    public void InitList()
    {
        audioList.Clear();

        foreach (var list in d_List)
        {
            audioList[list.e_State] = list.c_AudioClip;
        }
    }

    public async Task<AudioClip> Get(T state)
    {

        // キャッシュ済み
        if (cacheList.TryGetValue(state, out var cachedClip))
        {
            return cachedClip;
        }

        // 既にロード中
        if (loadingHandles.TryGetValue(state, out var loadingHandle))
        {
            await loadingHandle.Task;
            return loadingHandle.Result;
        }

        // AudioReference取得
        if (!audioList.TryGetValue(state, out var clipRef))
        {
            Debug.LogWarning($"{state}のClipが存在しない");
            return null;
        }

        // ロード開始
        var handle = clipRef.LoadAssetAsync<AudioClip>();

        loadingHandles[state] = handle;

        try
        {
            await handle.Task;
        }
        finally
        {
            loadingHandles.Remove(state);
        }

        //完了したら流す
        if (handle.Status is AsyncOperationStatus.Succeeded)
        {
            cacheList[state] = handle.Result;
            handleList[state] = handle;

            return handle.Result;
        }

        return null;

    }
    public void Release()
    {
        foreach (var handle in handleList.Values)
        {
            Addressables.Release(handle);
        }

        handleList.Clear();
        cacheList.Clear();
    }
}