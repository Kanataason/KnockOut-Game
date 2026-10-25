using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
[RequireComponent(typeof(Animator))]
public class PlayableAnimationPlayer : MonoBehaviour
{
    public Animator animator;

    //アニメーション再生にひつようなGraphを宣言
    private PlayableGraph baseGraph;
    private AnimationLayerMixerPlayable layerMixer;
    private AnimationClipPlayable clipGraph;

   private AvatarMask specialMask;
   private AvatarMask baseMask;
    [SerializeField] private float fadeDuration = 0.08f;

    private Coroutine _manualRoutine;
    private Coroutine _fadeCoroutine;

    private int eventIndexBuffer;//現在のイベント読み込み個数
    private void OnValidate()
    {
        if (!animator) animator = GetComponent<Animator>();
    }

    void InitGraph()
    {
        //PlayableGraph 初期化
        baseGraph = PlayableGraph.Create($"{gameObject.name}-graph");
        var output = AnimationPlayableOutput.Create(baseGraph, "AnimationOutput", animator);

        //AnimationLayerMixerPlayableを使う
        layerMixer = AnimationLayerMixerPlayable.Create(baseGraph, 3);
        output.SetSourcePlayable(layerMixer);

        //AnimatorControllerをInput0に接続
        var controllerPlayable = AnimatorControllerPlayable.Create(baseGraph, animator.runtimeAnimatorController);
        layerMixer.ConnectInput(0, controllerPlayable, 0);
        layerMixer.SetInputWeight(0, 1f);

        //AnimatorControllerを切り離し
        animator.runtimeAnimatorController = null;

        //MaskをInput1に設定しておく
        layerMixer.SetLayerMaskFromAvatarMask(1, specialMask);

        baseGraph.Play();
    }
    //アニメーションコントローラーから呼ばれてアバターマスクをセットしてグラフを初期化
    public void SetAvatarMask(AvatarMask normal,AvatarMask crouch)
    {
        specialMask = crouch;
        baseMask = normal;

        InitGraph();
    }

    public void SetPlayInfo(AnimationClip clip,AvatarMask mask=null)
    {
        StopManual();

        if (mask == null) mask = baseMask;
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
            layerMixer.SetInputWeight(1, 0f);
            layerMixer.SetInputWeight(0, 1f);
        }

        //アニメーションplayableを生成
        clipGraph = AnimationClipPlayable.Create(baseGraph, clip);
        clipGraph.SetDuration(clip.length);
        clipGraph.Pause();

        // Mixer に接続（Input1に入れる）
        layerMixer.ConnectInput(1, clipGraph, 0);
        layerMixer.SetInputWeight(0, 1f); // ベースモーション常に有効
        layerMixer.SetInputWeight(1, 1f);
        
        //レイヤーにアバターマスクを設定
        layerMixer.SetLayerMaskFromAvatarMask(1, mask);

    }

    //位置フレームずつ自分でアニメーションを再生
    public void InitAnimaInfo(int totalFrames, AnimationClip clip, AnimationClipPlayable playable = default)
    {
        eventIndexBuffer = 0;

        if (!playable.IsValid())
            playable = clipGraph;

        PlayAnimation(0,totalFrames, clip,playable);
    }
    public void PlayAnimation(int currentIndex,int totalFrames, AnimationClip clip,AnimationClipPlayable playable = default)
    {
        if (!clipGraph.IsValid())
        {
            return;
        }

        if (!playable.IsValid())
            playable = clipGraph;

        var events = clip.events;

        int eventIndex = eventIndexBuffer;

        if (currentIndex >= totalFrames)
        {
            EndPlayAnima();
            EndManual();
            return;
        }

        if (!playable.IsValid()) return;

        //現在の時間がclipのどのくらいまで再生しているのかを出している
        float normalizedTime = (float)currentIndex / totalFrames;

        float currentTime = normalizedTime * clip.length;

        //playableに現在の時間を入れる
        playable.SetTime(currentTime);

        //現在のフレームにアニメーションイベントがあるかを確認
        if (eventIndex < events.Length &&
                    events[eventIndex].time <= currentTime)
        {
            ExecuteAnimationEvent(events[eventIndex]);
            eventIndex++;
        }

        eventIndexBuffer = eventIndex;
    }
    public void EndPlayAnima()
    {
        if (!clipGraph.IsValid())
        {
            Debug.LogWarning("_clip is invalid");
            return;
        }
        var clip = clipGraph.GetAnimationClip();

        //最後の少し前のアニメーションを再生する
        clipGraph.SetTime(clip.length - 0.001f);
        baseGraph.Evaluate();

        eventIndexBuffer = 0;

    }
    private void ExecuteAnimationEvent(AnimationEvent animEvent)
    {
        // AnimationEvent の functionName を対象のGameObjectに送信
        SendMessage(animEvent.functionName, animEvent.objectReferenceParameter, SendMessageOptions.DontRequireReceiver);
    }
    public void StopManual()
    {
        if (_manualRoutine != null)
        {
            StopCoroutine(_manualRoutine);
            _manualRoutine = null;
        }

        if (clipGraph.IsValid())
        {
            clipGraph.Destroy();
        }

        if (layerMixer.GetInputCount() > 1 && layerMixer.GetInput(1).IsValid())
            layerMixer.SetInputWeight(1, 0f);

        if (layerMixer.GetInputCount() > 0 && layerMixer.GetInput(0).IsValid())
            layerMixer.SetInputWeight(0, 1f);
    }
    public void StopClip() 
    {
        if (clipGraph.IsValid()) clipGraph.Pause();
        if (baseGraph.IsValid()) baseGraph.Stop();
    }
    public void StartClip()
    {
        if (clipGraph.IsValid()) clipGraph.Play();
        if (baseGraph.IsValid()) baseGraph.Play();

    }
    public void EndManual()//playableアニメーション終了処理
    {
        if (_manualRoutine != null)
        {
            StopCoroutine(_manualRoutine);
            _manualRoutine = null;
        }
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeBackToBase());
    }


    private IEnumerator FadeBackToBase()//なめらかにノーマルアニメーションに戻す
    {
        float elapsed = 0f;
        float startWeight = layerMixer.GetInputWeight(1);

        while (elapsed < fadeDuration)
        {
            //レイヤーミキサーが稼働していなかったら流さない
            if (!layerMixer.IsValid())
                yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / fadeDuration;

            //なめらかにノーマルアニメーションに戻す
            layerMixer.SetInputWeight(1, Mathf.Lerp(startWeight, 0f, t));

            yield return null;
        }

        layerMixer.SetInputWeight(1, 0f);
        layerMixer.SetInputWeight(0, 1f);

        if (clipGraph.IsValid())
        {
            clipGraph.Destroy();
            clipGraph = default;
        }
        _fadeCoroutine = null;

    }

    private void OnDestroy()
    {
        //稼働していたら捨てる
        if (baseGraph.IsValid())
        {
            baseGraph.Destroy();
        }
    }
}
