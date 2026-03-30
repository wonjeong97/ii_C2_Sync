using System;
using System.Collections;
using My.Scripts.Core;
using UnityEngine;
using UnityEngine.UI;

namespace My.Scripts.UI
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class RedStringController : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private PlayerController player1;
        [SerializeField] private PlayerController player2;

        [Header("Visual Settings")]
        [SerializeField] private float thicknessNear = 25f; 
        [SerializeField] private float thicknessFar = 13f;
        [SerializeField] private float thicknessLerpSpeed = 10f;
        [SerializeField] private float nearYOffset = 30f;   
        
        [Header("Hit & Near Offsets")]
        [SerializeField] private float hitHeightNear = 44f;
        [SerializeField] private float hitHeightFar = 70f;
        [SerializeField] private float minWidthForIdx0 = 45f;

        [Header("Distance Sprites")]
        [SerializeField] private Sprite[] normalSprites; 
        [SerializeField] private Sprite hit1000;
        [SerializeField] private Sprite hit1400;

        private RectTransform _rectTransform;
        private Image _image;
        private Canvas _parentCanvas;
        private readonly float[] _thresholds = { 200f, 400f, 800f, 1000f, 1400f };
        private float _currentThickness;
        private bool _wasHit;

        /// <summary>
        /// 필수 컴포넌트들을 캐싱하고 예외 상황을 검사함.
        /// </summary>
        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            _parentCanvas = GetComponentInParent<Canvas>();

            if (!player1) Debug.LogWarning("Player1 컴포넌트 누락됨.");
            if (!player2) Debug.LogWarning("Player2 컴포넌트 누락됨.");
            if (!_parentCanvas) Debug.LogWarning("부모 Canvas 컴포넌트 누락됨.");
        }

        /// <summary>
        /// UI 초기 기준점 및 실 두께 초기화.
        /// </summary>
        private void Start()
        {
            // 이유: 실의 시작점을 왼쪽 끝으로 고정하여 길이와 회전 변환 시 앵커를 기준 축으로 삼음.
            _rectTransform.pivot = new Vector2(0f, 0.5f); 
            _currentThickness = thicknessFar;
        }

        /// <summary>
        /// 매 프레임 캐릭터들의 최종 위치가 결정된 후 실의 위치 및 모양을 갱신함.
        /// </summary>
        private void LateUpdate()
        {
            if (!player1 || !player2 || !_parentCanvas) return;
            UpdateLine();
        }

        /// <summary>
        /// 두 플레이어 사이의 거리와 각도를 계산하여 붉은 실 UI를 변형시킴.
        /// </summary>
        private void UpdateLine()
        {
            // # TODO: 매 프레임 발생하는 TransformPoint 변환 연산 최적화를 위해 캐싱 고려.
            Vector2 p1Pos = player1.CharacterRect.anchoredPosition;
            Vector2 p2Pos = player2.CharacterRect.anchoredPosition;
            
            // 이유: 플레이어의 좌우 위치가 역전될 때 실의 연결 방향을 자연스럽게 뒤집어 꼬임을 방지함.
            bool p1IsLeft = p1Pos.x <= p2Pos.x;

            // 예시 입력: p1(0,0), p2(300,400) -> 결과값 = 500
            float playerDist = Vector2.Distance(p1Pos, p2Pos);
            int idx = GetThresholdIndex(playerDist);

            Vector3 w1 = player1.GetHandUIPosition(p1IsLeft); 
            Vector3 w2 = player2.GetHandUIPosition(!p1IsLeft);

            Vector2 start = _rectTransform.parent.InverseTransformPoint(w1);
            Vector2 end = _rectTransform.parent.InverseTransformPoint(w2);

            bool isStunned = player1.IsStunned || player2.IsStunned;
            float targetHeight = (idx == 0) ? thicknessNear : thicknessFar;
            
            if (isStunned)
            {
                if (idx == 3) targetHeight = hitHeightNear;      
                else if (idx == 4) targetHeight = hitHeightFar; 
            }

            // 이유: 피격 상태일 때는 두께를 즉각 반영하고, 평시에는 부드럽게 보간하여 텐션 변화를 표현함.
            _currentThickness = (isStunned || _wasHit) 
                ? targetHeight 
                : Mathf.Lerp(_currentThickness, targetHeight, Time.deltaTime * thicknessLerpSpeed);
            
            _wasHit = isStunned;

            // 이유: 가장 가까운 거리일 때 실이 캐릭터 몸에 너무 가려지지 않도록 높이 오프셋을 적용함.
            if (idx == 0)
            {
                start.y -= nearYOffset;
                end.y -= nearYOffset;
            }

            Vector2 diff = end - start;
            float finalWidth = diff.magnitude;

            if (idx == 0)
            {
                finalWidth = Mathf.Max(finalWidth, minWidthForIdx0);
            }

            _rectTransform.anchoredPosition = start;
            _rectTransform.sizeDelta = new Vector2(finalWidth, _currentThickness);
            
            // 예시 입력: diff(100, 100) -> Atan2(PI/4) * Rad2Deg -> 결과값 = 45도
            _rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
            _rectTransform.localScale = p1IsLeft ? Vector3.one : new Vector3(1, -1, 1);

            UpdateSprite(idx, isStunned);
        }

        /// <summary>
        /// 계산된 거리 인덱스와 피격 상태에 맞춰 실의 텍스처(스프라이트)를 변경함.
        /// </summary>
        /// <param name="idx">거리 임계값 인덱스</param>
        /// <param name="isHit">피격 상태 여부</param>
        private void UpdateSprite(int idx, bool isHit)
        {
            if (normalSprites == null || normalSprites.Length == 0)
            {
                Debug.LogWarning("normalSprites 배열 데이터 누락됨.");
                _image.sprite = null;
                return;
            }

            int clampedIdx = Mathf.Clamp(idx, 0, normalSprites.Length - 1);
            if (idx >= normalSprites.Length)
            {
                Debug.LogWarning($"idx({idx})가 normalSprites 길이를 초과하여 {clampedIdx}로 클램핑됨.");
            }

            Sprite s = normalSprites[clampedIdx];
            
            if (isHit)
            {
                if (idx == 3)
                {
                    if (hit1000) s = hit1000;
                    else Debug.LogWarning("hit1000 스프라이트 누락됨.");
                }
                else if (idx == 4)
                {
                    if (hit1400) s = hit1400;
                    else Debug.LogWarning("hit1400 스프라이트 누락됨.");
                }
            }

            if (!s)
            {
                _image.sprite = null;
            }
            else if (_image.sprite != s)
            {
                _image.sprite = s;
            }
        }

        /// <summary>
        /// 현재 플레이어 간 거리가 어느 임계값 기준에 가장 가까운지 인덱스를 찾음.
        /// </summary>
        /// <param name="d">현재 두 플레이어의 UI 상 거리</param>
        /// <returns>가장 가까운 거리 임계값의 배열 인덱스</returns>
        private int GetThresholdIndex(float d)
        {
            int best = 0; 
            float min = float.MaxValue;
            
            for (int i = 0; i < _thresholds.Length; i++)
            {
                float diff = Mathf.Abs(d - _thresholds[i]);
                if (diff < min) 
                { 
                    min = diff; 
                    best = i; 
                }
            }
            return best;
        }
    }
}