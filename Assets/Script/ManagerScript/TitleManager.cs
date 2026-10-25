using System;
using UnityEngine;
using UnityEngine.VFX;

public class TitleManager : MonoBehaviour
{
    //タイトルのエフェクト
    public VisualEffect TitleVfx;

    //カウント用変数
    private float particleEmptyTime = 0;
    private float restartDelay = 0.25f;//パーティクルを消す時間
    private bool canInput = false;//入力可能かのフラグ

    //テキストを点滅させたり、シーンをロードのために通知するアクション
    public event Action OnTitleInput;
    void Start()
    {
        float waitTime = 1.5f;
        //1.5秒待つ
        Delay.WaitTime(this, waitTime, () =>
        {
            //ボイス再生
            AudioManager.Instance.PlayVoice(AudioManager.VoiceState.Title);
            canInput = true;
        });
    }
    private void Update()
    {
        if (TitleVfx == null||!canInput) return;

        if (Input.anyKeyDown)//何かボタンを押されたら
        {
            KillParticles();

            //押されたことを通知
            OnTitleInput?.Invoke();
        }

        UpdateVFX();
    }

    private void UpdateVFX()
    {
        if (TitleVfx.aliveParticleCount == 0)//パーティクルがなくなったら
        {
            particleEmptyTime += Time.deltaTime;

            if (particleEmptyTime >= restartDelay)
            {
                TitleVfx.Stop();
                TitleVfx.Play();
                particleEmptyTime = 0f;
            }
        }
        else
        {
            particleEmptyTime = 0f; // パーティクルがある間はリセット
        }
    }
    // Update is called once per frame

    public void KillParticles()//VFXの変数に値をセット
    {
        TitleVfx.SetBool("KIllparticl", true);
    }
}
