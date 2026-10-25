using System;
using System.Collections.Generic;
using UnityEngine;

public class UiUtility : MonoBehaviour
{
    /// <summary>
    /// Uiのテキスト、パネル、ボタンなどのステータスが入っているクラス
    /// </summary>
    [Serializable]
    public static class StateClass
    {
        //必要になるステータスを宣言する
        public enum UiEventType
        {
            GameStart,
            GameStartCancel,
            SetPlayerStart,
            SettingStart,
            SettingCancel,
            SettingDisplay,
            SettingVolume,
            SettingPanelCancel,
            ManualStart,
            ManualMove,
            ManualAttack,
            ManualUi,
            ManualPanelCancel,
            ManualCancel,
            MainGameStart,
            BattleStart,
            BattlePause,
            BattlePauseExit,
            BattlePauseToMenu,
            Result,
            SettingController,
            None
        }

        public enum SpecialEventState
        {
            Setting,
            Manual,
            Battle,
            None
        }
        public enum PanelState
        {
            GameMenu,
            SelectGameMenu,
            Manual,
            SelectCharacter,
            CharacterInfoPlayer1,
            CharacterInfoPlayer2,
            BattlePause,
            PlayerSet,
            MenuMove,
            MenuAttack,
            MenuUi,
            MainSetting,
            SettingDisplay,
            SettingVolume,
            Result,
            SettingController,
            None
        }

        public enum ButtonState
        {
            FirstGameSelect,
            FirstGameStart,
            FirstSettingStart,
            SelectCharacterStart,
            FirstBattlePause,
            FirstManual,
            FirstPlayerSet,
            SettingDisplay,
            SettingVolume,
            Result,
            SettingController,
            SettingSoloKeyBoard,
            None
        }
        public enum SliderState
        {
            ManualMove,
            ManualAttack,
            ManualUi
        }
        public enum TextState
        {
            CurrentController,
        }
    }

    public enum PauseReason { Menu, CameraZoom }

    /// <summary>
    /// 時間を止める理由をまとめて管理する Time.timeScaleはここ以外で書き換えない
    /// 理由が1つでも残っている間は止まったままにする（ズーム中にポーズを閉じても動き出さない）
    /// </summary>
    public static class PauseService
    {
        private static readonly HashSet<PauseReason> reasons = new();

        public static bool IsPaused => reasons.Count > 0;

        public static void Pause(PauseReason reason) { reasons.Add(reason); Apply(); }
        public static void Resume(PauseReason reason) { reasons.Remove(reason); Apply(); }

        /// <summary>
        /// シーンを移る時など、すべての理由を消して時間を戻す
        /// </summary>
        public static void ResumeAll() { reasons.Clear(); Apply(); }

        private static void Apply()
        {
            Time.timeScale = IsPaused ? 0 : 1;
        }

        //ドメインのリロードを切っていても前回のプレイの理由が残らないようにする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => reasons.Clear();
    }


    /// <summary>
    /// 画面に影響がある関数のクラス
    /// </summary>
    public static class DisplayEffect
    {
        /// <summary>
        /// ズームをさせる
        /// </summary>
        public static void ZoomIn(MonoBehaviour owner, Camera camera, float duration = 1f)
        {
            if (camera == null) return;
            owner.StartCoroutine(UpdateZoomIn(camera, duration));
        }
        private static System.Collections.IEnumerator UpdateZoomIn(Camera camera, float duration)
        {
            float targetFov = 1f;
            float startFov = camera.fieldOfView;
            float t = 0f;
            
            //渡されたdurationまで処理をする
            while (t < duration)
            {
                t += Time.deltaTime;

                //カメラの焦点を縮めていく
                camera.fieldOfView =
                    Mathf.Lerp(startFov, targetFov, t / duration);

                yield return null;
            }

            //最終的な焦点を合わせる
            camera.fieldOfView = targetFov;
        }

        /// <summary>
        /// 渡された数値によってフェードさせる
        /// </summary>
        public static void Fade(MonoBehaviour owner,UnityEngine.UI.Image fadeImage, float startAlpha, float endAlpha, float duration = 0.5f)
        {
            if (fadeImage == null) return;

            owner.StartCoroutine(
                UpdateFade(fadeImage, startAlpha, endAlpha, duration));
        }

        private static System.Collections.IEnumerator UpdateFade(UnityEngine.UI.Image fadeImage, float startAlpha, float endAlpha, float duration)
        {
            float t = 0f;
            Color c = fadeImage.color;

            while (t < duration)
            {
                t += Time.deltaTime;

                //渡されたスタートのアルファ値に応じてfadeOutとfadeInかを分ける
                float alpha = Mathf.Lerp(
                    startAlpha,
                    endAlpha,
                    t / duration);

                fadeImage.color =
                    new Color(c.r, c.g, c.b, alpha);

                yield return null;
            }

            fadeImage.color =
                new Color(c.r, c.g, c.b, endAlpha);
        }
    }
}
