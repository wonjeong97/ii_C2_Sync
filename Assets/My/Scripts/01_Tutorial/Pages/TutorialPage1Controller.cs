using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.Global;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using VContainer;
using Wonjeong.Data;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._01_Tutorial.Pages
{
    [Serializable]
    public class TutorialPage1Data
    {
        public TextSetting descriptionText;
    }

    public class TutorialPage1Controller : GamePage<TutorialPage1Data>
    {
        [Header("Page 1 UI")]
        [SerializeField] private Text descriptionText;
        [SerializeField] private APIManager apiManager;

        [Header("Polling Settings")]
        [SerializeField] private float pollInterval = 3.0f;

        private CancellationTokenSource _cts;
        private ILogger<TutorialPage1Controller> _logger;
        private GameManager _gameManager;
        private SoundManager _soundManager;
        private UIManager _uiManager;
        private SessionManager _sessionManager;

        [Inject]
        public void Construct(
            ILogger<TutorialPage1Controller> logger,
            GameManager gameManager,
            SoundManager soundManager,
            UIManager uiManager,
            SessionManager sessionManager)
        {
            _logger = logger;
            _gameManager = gameManager;
            _soundManager = soundManager;
            _uiManager = uiManager;
            _sessionManager = sessionManager;
        }

        protected override void Awake()
        {
            base.Awake();
            SetTextAlpha(0f);
        }

        protected override void SetupData(TutorialPage1Data data)
        {
            if (descriptionText && _uiManager)
            {
                _uiManager.SetText(descriptionText.gameObject, data.descriptionText);
            }
            else
            {
                _logger?.ZLogWarning($"[TutorialPage1] UI 컴포넌트 또는 UIManager 누락.");
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            var cts = new CancellationTokenSource();
            _cts = cts;

            if (_gameManager) _gameManager.IsAutoProgressing = true;

            FadeInAndPollAsync(cts).Forget();
        }

        public override void OnExit()
        {
            if (_soundManager)
            {
                _soundManager.StopBGM();
                _soundManager.PlayBGM("MainBGM");
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            base.OnExit();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return)) CompleteStep();
        }

        private async UniTaskVoid FadeInAndPollAsync(CancellationTokenSource cts)
        {
            CancellationToken ct = cts.Token;
            if (descriptionText)
            {
                float duration = 0.5f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    SetTextAlpha(Mathf.Clamp01(elapsed / duration));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                SetTextAlpha(1f);
            }

            await PollRoomStateAsync(cts);
        }

        private async UniTask PollRoomStateAsync(CancellationTokenSource cts)
        {
            CancellationToken ct = cts.Token;
#if UNITY_EDITOR
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            if (apiManager) apiManager.FillDebugSession();
            CompleteStep();
            return;
#endif
            // 유저가 마지막으로 확인된 적 없는 상태가 시작된 시각 (-1 = 아직 빈 상태 아님)
            float emptyStartTime = -1f;

            while (!ct.IsCancellationRequested)
            {
                if (_gameManager?.ApiConfig == null)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(pollInterval), cancellationToken: ct);
                    continue;
                }

                string code = GameConstants.Module.Code.ToLower();
                string checkUrl = ZString.Concat(_gameManager.ApiConfig.CheckRoomStateUrl, "?code=", code);
                string userUrl = ZString.Concat(_gameManager.ApiConfig.GetCurrentRoomUserUrl, "?code=", code);

                // 방 상태 확인
                bool roomEmpty = false;
                using (var req = UnityWebRequest.Get(checkUrl))
                {
                    req.timeout = 10;
                    UnityWebRequest res = null;
                    try
                    {
                        res = await req.SendWebRequest().ToUniTask(cancellationToken: ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (UnityWebRequestException ex)
                    {
                        _logger?.ZLogWarning($"checkUrl 통신 오류: {ex.Message}. 재시도합니다.");
                        await UniTask.Delay(TimeSpan.FromSeconds(pollInterval), cancellationToken: ct);
                        continue;
                    }

                    if (res.result == UnityWebRequest.Result.Success &&
                        res.downloadHandler.text.Contains(GameConstants.Api.StatusEmpty,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        roomEmpty = true;
                    }
                }

                if (roomEmpty)
                {
                    emptyStartTime = (emptyStartTime < 0f) ? Time.time : emptyStartTime;
                    if (Time.time - emptyStartTime >= 15f)
                    {
                        cts.Cancel();
                        _gameManager.ReturnToTitle();
                        return;
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(pollInterval), cancellationToken: ct);
                    continue;
                }

                // 유저 조회 (방이 USING일 때만)
                using (var req = UnityWebRequest.Get(userUrl))
                {
                    req.timeout = 10;
                    UnityWebRequest res = null;
                    try
                    {
                        res = await req.SendWebRequest().ToUniTask(cancellationToken: ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (UnityWebRequestException ex)
                    {
                        _logger?.ZLogWarning($"userUrl 통신 오류: {ex.Message}. 재시도합니다.");
                        await UniTask.Delay(TimeSpan.FromSeconds(pollInterval), cancellationToken: ct);
                        continue;
                    }

                    if (res.result == UnityWebRequest.Result.Success)
                    {
                        string rawText = res.downloadHandler.text;
                        if (rawText.Contains(GameConstants.Api.StatusEmpty, StringComparison.OrdinalIgnoreCase))
                        {
                            emptyStartTime = (emptyStartTime < 0f) ? Time.time : emptyStartTime;
                            if (Time.time - emptyStartTime >= 15f)
                            {
                                cts.Cancel();
                                _gameManager.ReturnToTitle();
                                return;
                            }
                        }
                        else if (rawText.Contains(","))
                        {
                            emptyStartTime = -1f;
                            string[] parts = rawText.Split(new[] { '\r', '\n', ',' },
                                StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && _sessionManager)
                            {
                                _sessionManager.PlayerAUid = parts[0].Trim();
                                _sessionManager.PlayerBUid = parts[1].Trim();
                                var fetchResult = await apiManager.FetchDataAsync(parts[0].Trim())
                                    .SuppressCancellationThrow();
                                if (fetchResult.IsCanceled == false && fetchResult.Result == true &&
                                    _sessionManager.CurrentUserId != 0)
                                {
                                    CompleteStep();
                                    return;
                                }
                            }
                        }
                    }
                }

                await UniTask.Delay(TimeSpan.FromSeconds(pollInterval), cancellationToken: ct);
            }
        }

        private void SetTextAlpha(float alpha)
        {
            if (descriptionText)
            {
                Color c = descriptionText.color;
                c.a = alpha;
                descriptionText.color = c;
            }
        }
    }
}