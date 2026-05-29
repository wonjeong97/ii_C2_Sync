using System;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using My.Scripts.Core;
using My.Scripts.Hardware;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using VContainer;
using Wonjeong.UI;
using ZLogger;

namespace My.Scripts._00_Title
{
    public class TitleManager : MonoBehaviour
    {
        [Header("Polling Settings")]
        [SerializeField] private float pollInterval = 3.0f; 

        private bool _isTransitioning; 
        private CancellationTokenSource _destroyCts;

        private ILogger<TitleManager> _logger;
        private ArduinoManager _arduinoManager;
        private GameManager _gameManager;
        private SoundManager _soundManager;

        [Inject]
        public void Construct(
            ILogger<TitleManager> logger,
            ArduinoManager arduinoManager,
            GameManager gameManager,
            SoundManager soundManager)
        {
            _logger = logger;
            _arduinoManager = arduinoManager;
            _gameManager = gameManager;
            _soundManager = soundManager;
        }

        private void Awake()
        {
            _destroyCts = new CancellationTokenSource();
        }

        private void Start()
        {
            InitializeTitleSystem();
        }

        private void InitializeTitleSystem()
        {
            CancellationToken token = _destroyCts.Token;

            if (_arduinoManager) _arduinoManager.Reconnect();
            else _logger?.ZLogWarning($"ArduinoManager가 주입되지 않았습니다.");

            PlayMainBgmAsync(token).Forget();
            PollRoomStateAsync(token).Forget();
        }

        private void OnDestroy()
        {
            _destroyCts?.Cancel();
            _destroyCts?.Dispose();
        }

        private async UniTaskVoid PollRoomStateAsync(CancellationToken ct)
        {
#if UNITY_EDITOR
            _logger?.ZLogInformation($"에디터 모드: API 폴링 생략.");
            return;
#endif
            while (!_isTransitioning && !ct.IsCancellationRequested)
            {
                if (_gameManager?.ApiConfig == null)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(pollInterval), cancellationToken: ct);
                    continue;
                }

                // ZString 최적화 적용: 문자열 보간 대신 Concat 사용
                string requestUrl = ZString.Concat(_gameManager.ApiConfig.CheckRoomStateUrl, "?code=c2");

                using (UnityWebRequest webRequest = UnityWebRequest.Get(requestUrl))
                {
                    webRequest.timeout = 10;
                    
                    var result = await webRequest.SendWebRequest().ToUniTask(cancellationToken: ct);
                    
                    if (result.result == UnityWebRequest.Result.Success)
                    {
                        if (result.downloadHandler.text.Contains("USING", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.ZLogInformation($"RoomState USING 감지. 튜토리얼로 이동.");
                            GoToTutorial();
                            return;
                        }
                    }
                    else
                    {
                        _logger?.ZLogWarning($"상태 체크 통신 실패: {result.error}. 재시도합니다.");
                    }
                }

                await UniTask.Delay(TimeSpan.FromSeconds(pollInterval), cancellationToken: ct);
            }
        }

        private void Update()
        {
            if (_isTransitioning) return; 

            if (Input.GetKeyDown(KeyCode.Return))
            {
                GoToTutorial();
            }
        }

        private void GoToTutorial()
        {
            if (_isTransitioning) return;
            _isTransitioning = true; 
            SceneManager.LoadScene(GameConstants.Scene.Tutorial);
        }
        
        private async UniTaskVoid PlayMainBgmAsync(CancellationToken ct)
        {
            if (!_soundManager) return;

            _soundManager.StopBGM();
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: ct);
            _soundManager.PlayBGM("MainBGM");
        }
    }
}