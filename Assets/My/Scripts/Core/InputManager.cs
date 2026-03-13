using System;
using UnityEngine;
using My.Scripts.Hardware;

namespace My.Scripts.Core
{
    /// <summary>
    /// 하드웨어 입력(아두이노 발판 및 키보드 디버그)을 감지하고 게임 플레이 이벤트로 변환하여 전파하는 매니저 클래스.
    /// 입력 장치와 게임 로직을 분리(Decoupling)하여, 추후 입력 방식이 변경되더라도 로직 수정 없이 이곳만 수정하면 되도록 함.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance;

        /// <summary>
        /// 발판이 눌렸을 때 발생하는 이벤트.
        /// (PlayerIndex, LaneIndex, PadIndex) 정보를 전달함.
        /// </summary>
        public event Action<int, int, int> OnPadDown;

        /// <summary>
        /// 발판에서 발을 뗐을 때 발생하는 이벤트.
        /// </summary>
        public event Action<int, int, int> OnPadUp;

        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 아두이노 매니저가 존재할 경우 하드웨어 입력 이벤트를 구독하여 실물 발판 신호를 받음
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput += HandleArduinoInput;
            }
        }

        private void OnDestroy()
        {
            // 메모리 누수 및 에러 방지를 위해 오브젝트 파괴 시 이벤트 구독 해제
            if (ArduinoManager.Instance)
            {
                ArduinoManager.Instance.OnHardwareInput -= HandleArduinoInput;
            }
        }

        private void Update()
        {
            // 매 프레임 키 입력을 폴링(Polling)하여 이벤트를 발생시킴
            // 하드코딩된 키 매핑을 사용하며, 아두이노가 연결되지 않은 개발 환경에서의 테스트를 위함임

            // === Player A (ID: 0) ===
            CheckInput(KeyCode.Alpha1, 0, 0, 0); // 1번 패드
            CheckInput(KeyCode.Alpha2, 0, 0, 1); // 2번 패드
            
            CheckInput(KeyCode.Alpha3, 0, 1, 0); // 3번 패드
            CheckInput(KeyCode.Alpha4, 0, 1, 1); // 4번 패드

            CheckInput(KeyCode.Alpha5, 0, 2, 0); // 5번 패드
            CheckInput(KeyCode.Alpha6, 0, 2, 1); // 6번 패드

            // === Player B (ID: 1) ===
            CheckInput(KeyCode.Alpha7, 1, 0, 0); // 7번 패드
            CheckInput(KeyCode.Alpha8, 1, 0, 1); // 8번 패드

            CheckInput(KeyCode.Alpha9, 1, 1, 0); // 9번 패드
            CheckInput(KeyCode.Alpha0, 1, 1, 1); // 10번 패드

            CheckInput(KeyCode.Minus, 1, 2, 0);  // 11번 패드
            CheckInput(KeyCode.Equals, 1, 2, 1); // 12번 패드
        }

        /// <summary>
        /// 특정 키의 입력 상태를 확인하고 이벤트를 트리거함.
        /// </summary>
        private void CheckInput(KeyCode key, int playerIdx, int laneIdx, int padIdx)
        {
            if (Input.GetKeyDown(key))
            {
                OnPadDown?.Invoke(playerIdx, laneIdx, padIdx);
            }

            if (Input.GetKeyUp(key))
            {
                OnPadUp?.Invoke(playerIdx, laneIdx, padIdx);
            }
        }

        /// <summary>
        /// 아두이노에서 수신된 단일 번호(1~12)를 인게임 물리 엔진과 UI가 처리할 수 있는 (Player, Lane, Pad) 3차원 인덱스로 분해함.
        /// 이유: 단일 스위치 번호만으로는 어떤 플레이어의 어느 방향 발판인지 알 수 없기 때문.
        /// </summary>
        private void HandleArduinoInput(int padNumber, bool isDown)
        {
            // 유효하지 않은 신호 필터링
            if (padNumber < 1 || padNumber > 12) return;

            // 수학적 인덱스 계산을 위해 0 기반 번호로 변경 (0 ~ 11)
            int zeroBasedPad = padNumber - 1;

            // 1~6번(인덱스 0~5)은 P1(0), 7~12번(인덱스 6~11)은 P2(1)
            int playerIdx = zeroBasedPad / 6;

            // 각 플레이어당 6개의 패드를 2개씩 묶어 3개의 라인(0:Left, 1:Center, 2:Right)으로 구분
            int laneIdx = (zeroBasedPad % 6) / 2;

            // 묶인 2개의 패드 중 홀수(0)와 짝수(1)를 구분
            int padIdx = zeroBasedPad % 2;

            // 파싱된 데이터를 기존의 키보드 동작과 동일한 이벤트 파이프라인에 태움
            if (isDown)
            {
                OnPadDown?.Invoke(playerIdx, laneIdx, padIdx);
            }
            else
            {
                OnPadUp?.Invoke(playerIdx, laneIdx, padIdx);
            }
        }
    }
}