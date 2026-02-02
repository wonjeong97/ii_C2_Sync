using UnityEngine;

public class SpriteFrameMover : MonoBehaviour
{
    [Header("Destination Settings")]
    public Vector3 endPosition = new Vector3(0, 0, -10); // 목표 위치 (끝 값)
    public float moveSpeed = 5.0f; // 이동 속도

    [Header("Debug Settings")]
    public KeyCode debugKey = KeyCode.Space; // 시작/정지 키
    public bool loopMovement = true; // 끝에 도달하면 다시 시작 위치로 보낼지 여부

    private Vector3 _startPosition;
    private bool _isMoving = false;

    void Start()
    {
        // 게임 시작 시 배치된 위치를 '시작 위치'로 기억
        _startPosition = transform.position;
    }

    void Update()
    {
        // 디버그 키 입력 시 이동 상태 토글 (켜기/끄기)
        if (Input.GetKeyDown(debugKey))
        {
            _isMoving = !_isMoving;
            Debug.Log($"[SpriteMover] 이동 상태: {_isMoving}");
        }

        // 이동 로직
        if (_isMoving)
        {
            // 현재 위치에서 endPosition을 향해 일정한 속도로 이동
            transform.position = Vector3.MoveTowards(transform.position, endPosition, moveSpeed * Time.deltaTime);

            // 목표 지점에 도달했는지 확인
            if (transform.position == endPosition)
            {
                if (loopMovement)
                {
                    // 반복 설정 시: 다시 시작 위치로 순간 이동 (무한 터널 효과용)
                    transform.position = _startPosition;
                }
                else
                {
                    // 반복 아님: 정지
                    _isMoving = false;
                    Debug.Log("[SpriteMover] 도착 완료");
                }
            }
        }
    }
    
    // 에디터에서 이동 경로를 선으로 보여줌 (디버깅용)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        // 현재 위치(또는 시작점)에서 목표 지점까지 선 그리기
        Vector3 from = Application.isPlaying ? _startPosition : transform.position;
        Gizmos.DrawLine(from, endPosition);
        Gizmos.DrawWireSphere(endPosition, 0.5f);
    }
}