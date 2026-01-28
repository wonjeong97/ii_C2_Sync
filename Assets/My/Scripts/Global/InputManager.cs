using System;
using UnityEngine;
using Wonjeong.Utils; // 템플릿의 유틸 사용 (싱글톤 등)

namespace My.Scripts.Global
{
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance;

        // 발판 입력 이벤트 (플레이어 ID, 라인 인덱스, 발판 인덱스)
        // playerIndex: 0=A, 1=B
        // laneIndex: 0=Left, 1=Center, 2=Right
        // padIndex: 0=첫번째, 1=두번째
        public event Action<int, int, int> OnPadDown;
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
            // === Player A (ID: 0) ===
            // Left (Lane 0)
            CheckInput(KeyCode.Alpha1, 0, 0, 0);
            CheckInput(KeyCode.Alpha2, 0, 0, 1);
            
            // Center (Lane 1)
            CheckInput(KeyCode.Alpha3, 0, 1, 0);
            CheckInput(KeyCode.Alpha4, 0, 1, 1);

            // Right (Lane 2)
            CheckInput(KeyCode.Alpha5, 0, 2, 0);
            CheckInput(KeyCode.Alpha6, 0, 2, 1);


            // === Player B (ID: 1) ===
            // Left (Lane 0)
            CheckInput(KeyCode.Alpha7, 1, 0, 0);
            CheckInput(KeyCode.Alpha8, 1, 0, 1);

            // Center (Lane 1)
            CheckInput(KeyCode.Alpha9, 1, 1, 0);
            CheckInput(KeyCode.Alpha0, 1, 1, 1);

            // Right (Lane 2)
            CheckInput(KeyCode.Minus, 1, 2, 0); // - 키
            CheckInput(KeyCode.Equals, 1, 2, 1); // = 키
        }

        /// <summary> 키 입력 상태를 체크하고 이벤트 발생 </summary>
        private void CheckInput(KeyCode key, int playerIdx, int laneIdx, int padIdx)
        {
            if (Input.GetKeyDown(key))
            {
                // 디버그 로그 (필요시 주석 해제)
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