using System;
using Microsoft.Extensions.Logging;
using My.Scripts.Hardware;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts.Core
{
    /// <summary>
    /// 하드웨어 입력(아두이노 및 키보드)을 감지하고 게임 이벤트로 전파하는 관리자 클래스.
    /// VContainer를 통한 의존성 주입 구조를 사용합니다.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public event Action<int, int, int> OnPadDown;
        public event Action<int, int, int> OnPadUp;

        private ArduinoManager _arduinoManager;
        private ILogger<InputManager> _logger;

        [Inject]
        public void Construct(ILogger<InputManager> logger, ArduinoManager arduinoManager)
        {
            _logger = logger;
            _arduinoManager = arduinoManager;
        }

        private void Start()
        {
            if (_arduinoManager != null)
            {
                _arduinoManager.OnHardwareInput += HandleArduinoInput;
            }
            else
            {
                _logger.ZLogWarning($"ArduinoManager가 컨테이너에 등록되지 않았습니다. 하드웨어 입력이 비활성화됩니다.");
            }
        }

        private void OnDestroy()
        {
            if (_arduinoManager != null)
            {
                _arduinoManager.OnHardwareInput -= HandleArduinoInput;
            }
        }

        private void Update()
        {
            // === Player A (ID: 0) ===
            CheckInput(KeyCode.Alpha1, 0, 0, 0); 
            CheckInput(KeyCode.Alpha2, 0, 0, 1); 
            CheckInput(KeyCode.Alpha3, 0, 1, 0); 
            CheckInput(KeyCode.Alpha4, 0, 1, 1); 
            CheckInput(KeyCode.Alpha5, 0, 2, 0); 
            CheckInput(KeyCode.Alpha6, 0, 2, 1); 

            // === Player B (ID: 1) ===
            CheckInput(KeyCode.Alpha7, 1, 0, 0); 
            CheckInput(KeyCode.Alpha8, 1, 0, 1); 
            CheckInput(KeyCode.Alpha9, 1, 1, 0); 
            CheckInput(KeyCode.Alpha0, 1, 1, 1); 
            CheckInput(KeyCode.Minus, 1, 2, 0);  
            CheckInput(KeyCode.Equals, 1, 2, 1); 
        }

        private void CheckInput(KeyCode key, int playerIdx, int laneIdx, int padIdx)
        {
            if (Input.GetKeyDown(key))
            {
                OnPadDown?.Invoke(playerIdx, laneIdx, padIdx);
            }
            else if (Input.GetKeyUp(key))
            {
                OnPadUp?.Invoke(playerIdx, laneIdx, padIdx);
            }
        }

        private void HandleArduinoInput(int padNumber, bool isDown)
        {
            // 1~12번 패드에 대한 매핑 로직 (0-based indexing)
            if (padNumber < 1 || padNumber > 12) return;

            int zeroBasedPad = padNumber - 1;
            int playerIdx = zeroBasedPad / 6;       // 0~5: P1, 6~11: P2
            int laneIdx = (zeroBasedPad % 6) / 2;   // 2개씩 묶어 Lane 구분
            int padIdx = zeroBasedPad % 2;          // 0 또는 1

            if (isDown) OnPadDown?.Invoke(playerIdx, laneIdx, padIdx);
            else OnPadUp?.Invoke(playerIdx, laneIdx, padIdx);
        }
    }
}