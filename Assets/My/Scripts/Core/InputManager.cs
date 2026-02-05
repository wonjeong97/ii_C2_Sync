using System;
using UnityEngine;

namespace My.Scripts.Core
{
    /// <summary>
    /// 하드웨어 입력(키보드 등)을 감지하고 게임 플레이 이벤트로 변환하여 전파하는 매니저 클래스.
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
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // 매 프레임 키 입력을 폴링(Polling)하여 이벤트를 발생시킴
            // 하드코딩된 키 매핑을 사용하며, 이는 물리적인 아케이드 발판의 매핑과 일치시킴

            // === Player A (ID: 0) ===
            // 좌측 라인 (Lane 0) - 키: 1, 2
            CheckInput(KeyCode.Alpha1, 0, 0, 0);
            CheckInput(KeyCode.Alpha2, 0, 0, 1);
            
            // 중앙 라인 (Lane 1) - 키: 3, 4
            CheckInput(KeyCode.Alpha3, 0, 1, 0);
            CheckInput(KeyCode.Alpha4, 0, 1, 1);

            // 우측 라인 (Lane 2) - 키: 5, 6
            CheckInput(KeyCode.Alpha5, 0, 2, 0);
            CheckInput(KeyCode.Alpha6, 0, 2, 1);


            // === Player B (ID: 1) ===
            // 좌측 라인 (Lane 0) - 키: 7, 8
            CheckInput(KeyCode.Alpha7, 1, 0, 0);
            CheckInput(KeyCode.Alpha8, 1, 0, 1);

            // 중앙 라인 (Lane 1) - 키: 9, 0
            CheckInput(KeyCode.Alpha9, 1, 1, 0);
            CheckInput(KeyCode.Alpha0, 1, 1, 1);

            // 우측 라인 (Lane 2) - 키: -, =
            CheckInput(KeyCode.Minus, 1, 2, 0); 
            CheckInput(KeyCode.Equals, 1, 2, 1); 
        }

        /// <summary>
        /// 특정 키의 입력 상태를 확인하고 이벤트를 트리거함.
        /// 중복 코드를 줄이고 가독성을 높이기 위한 헬퍼 메서드.
        /// </summary>
        /// <param name="key">감지할 키코드</param>
        /// <param name="playerIdx">플레이어 ID (0 or 1)</param>
        /// <param name="laneIdx">라인 인덱스 (0:Left, 1:Center, 2:Right)</param>
        /// <param name="padIdx">발판 인덱스 (0 or 1)</param>
        private void CheckInput(KeyCode key, int playerIdx, int laneIdx, int padIdx)
        {
            if (Input.GetKeyDown(key))
            {
                // 디버그용 로그 (필요 시 주석 해제하여 입력 확인 가능)
                // Debug.Log($"[Input] P{playerIdx} Lane{laneIdx} Pad{padIdx} DOWN");
                OnPadDown?.Invoke(playerIdx, laneIdx, padIdx);
            }

            if (Input.GetKeyUp(key))
            {
                OnPadUp?.Invoke(playerIdx, laneIdx, padIdx);
            }
        }
    }
}