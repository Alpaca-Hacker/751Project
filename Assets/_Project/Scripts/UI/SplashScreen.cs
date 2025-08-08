using System;
using System.Collections;
using AdvancedSceneManager.Loading;
using AdvancedSceneManager.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace AdvancedSceneManager.Defaults
{

    /// <summary>A default splash screen script. Fades splash screen in and out.</summary>
    [ExecuteAlways]
    [AddComponentMenu("Splash Screen Alpaca")]
    public class SplashScreenAlpaca : SplashScreen
    {
        public CanvasGroup groupBackground;
        public CanvasGroup groupLogo;
        public Image background;
        public Image logo;

        public Camera _camera;
        
        #region Skip splash screen

        void CheckIfSkip()
        {
            //Skip splash screen when any of the buttons are pressed
            if (IsSkipButtonPressed())
            {
                hasSkipped = true;
                coroutine?.Stop();
            }
        }

        bool IsSkipButtonPressed()
        {

            //softSkipSplashScreen is used to run unit tests quicker, and is otherwise unused.
            //You may remove this check when copying script.
            if (SceneManager.app.startupProps?.softSkipSplashScreen ?? false)
                return true;

#if INPUTSYSTEM
            return (UnityEngine.InputSystem.Keyboard.current?.spaceKey?.isPressed ?? false) ||
                (UnityEngine.InputSystem.Keyboard.current?.escapeKey?.isPressed ?? false) ||
                (UnityEngine.InputSystem.Mouse.current?.leftButton?.isPressed ?? false) ||
                (UnityEngine.InputSystem.Gamepad.current?.aButton?.isPressed ?? false);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButton(0);
#endif

        }

        #endregion

        protected override void Start()
        {

            base.Start();

            //Use same colour as unity splash screen, if enabled, defaults to black otherwise
            background.color = SceneManager.app.startupProps?.effectiveFadeColor ?? Color.black;

            if (Application.isPlaying)
            {

                groupLogo.alpha = 0;
                groupBackground.alpha = 0;

#if !UNITY_EDITOR
                if (!SceneManager.app.isRestart)
                    groupBackground.alpha = 1;
#endif

            }

        }

        void Update() =>
            CheckIfSkip();

        bool hasSkipped;
        GlobalCoroutine coroutine;

        public override IEnumerator OnOpen()
        {

            yield return RunCoroutine(groupBackground.Fade(1, 1));
            yield return RunCoroutine(Delay(0.5f));
            yield return RunCoroutine(groupLogo.Fade(1, 3f));

            yield return RunCoroutine(Delay(0.5f));
            _camera.enabled = false;

        }

        public override IEnumerator OnClose()
        {
            yield return RunCoroutine(groupLogo.Fade(0, 1f));
            yield return RunCoroutine(groupBackground.Fade(0, 1f));
            canvas.enabled = false;
        }

        GlobalCoroutine RunCoroutine(IEnumerator coroutine)
        {
            if (!hasSkipped)
                return this.coroutine = coroutine.StartCoroutine();
            else
                return null;
        }

        IEnumerator Delay(float by)
        {
            yield return new WaitForSecondsRealtime(by);
        }

        IEnumerator Delay(Func<bool> until)
        {
            yield return new WaitUntil(() => hasSkipped || until());
        }

    }

}
