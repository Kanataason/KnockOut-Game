using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;

public class PlayerEffectController : MonoBehaviour
{

    [FormerlySerializedAs("efectList")] [SerializeField] private AddressableObjectPool<EffectType> effectList;

    private bool isLoaded = false;

    /// <summary>
    /// エフェクトの読み込みが終わったか（失敗した場合も終わった扱い）
    /// </summary>
    public bool IsLoadFinished { get; private set; } = false;

    async void Start()
    {
        //async voidの例外は握りつぶされるのでここでログに出す
        try
        {
            //エフェクトを生成
            await effectList.Init(transform);

            isLoaded = true;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            //失敗してもバトルの初期化が止まらないように終わったことにする
            IsLoadFinished = true;
        }
    }

    private void OnDestroy()
    {
        effectList.Release();
    }


    //演出用のエフェクトのタイプを確認
    public EffectType CanPlayPresentationEffect(AnimationManager.PresentationState presentationState)
    {
        EffectType type = presentationState switch
        {
            AnimationManager.PresentationState.Charge => EffectType.Charge,
            _ => EffectType.None
        };

        return type;
    }

    public void PlayEffect(EffectType type,Vector3 pos,Quaternion rotation,int returnFrame)
    {
        if (!isLoaded) return;
        var effect = effectList.Get(type);

        if (effect == null) return;

        //場所と回転をセット
        SetPosAndRotation(effect, pos, rotation);

        //エフェクトを消すコルーチン
        Delay.WaitFrame(this, returnFrame,null,null,() => { ReturnEffect(type, effect); });

    }
    public void ReturnEffect(EffectType type,GameObject obj)
    {
        if (!isLoaded) return;

        //エフェクトを戻す
        effectList.Return(type, obj);
    }
    public void PlayVfxEffectAndGetVfxEffect(EffectType effectType,Transform root,
        Quaternion rotation,Action<GameObject> receiver)
    {
        var effect = effectList.Get(effectType);
        //場所や回転を設定
        effect.transform.position = root.position;
        effect.transform.rotation = rotation;

        var vis = effect.GetComponent<VisualEffect>();

        //設定が終わったことを通知
        receiver?.Invoke(effect);
    }
    /// <summary>
    /// 位置と回転を設定
    /// </summary>
    private void SetPosAndRotation(GameObject effect,Vector3 pos,Quaternion rotation)
    {
        effect.transform.position = pos;
        effect.transform.rotation = rotation;
    }

}
