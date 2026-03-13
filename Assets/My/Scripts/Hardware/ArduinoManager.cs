using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using My.Scripts.Core; 
using My.Scripts.Global;
using Wonjeong.Utils;

namespace My.Scripts.Hardware
{
    /// <summary>
    /// C2 콘텐츠 전용 단일 아두이노 매니저입니다.
    /// 연결된 아두이노에서 전송하는 "1 On" ~ "12 Off" 형식의 발판 시그널을 수신하여 이벤트를 발생시킵니다.
    /// </summary>
    public class ArduinoManager : MonoBehaviour
    {
        public static ArduinoManager Instance;

        /// <summary> 
        /// 하드웨어 발판 입력 발생 시 구독 중인 클래스로 신호를 전달하는 이벤트.
        /// (int padNumber: 1~12, bool isDown: 눌림 여부)
        /// </summary>
        public Action<int, bool> OnHardwareInput;

        private SerialPort _arduinoPort;

        // 백그라운드 스레드에서 수신된 데이터를 메인 스레드로 안전하게 넘기기 위한 스레드-세이프 큐
        private ConcurrentQueue<(int padNumber, bool isDown)> _inputQueue =
            new ConcurrentQueue<(int padNumber, bool isDown)>();

        private Thread _readThread;
        private bool _isRunning = false;
        
        // 비동기 작업 안전 종료를 위한 취소 토큰 소스
        private CancellationTokenSource _cts;

        // 예외 로그 스로틀링용 변수
        private DateTime _lastWarnTime = DateTime.MinValue;
        private readonly TimeSpan WarnThrottle = TimeSpan.FromSeconds(5);

        public bool IsConnected => _arduinoPort != null && _arduinoPort.IsOpen;

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
                return;
            }
        }

        private void Start()
        {
            _isRunning = true;
            _cts = new CancellationTokenSource();
            
            // 토큰을 전달하여 씬 전환 및 객체 파괴 시 비동기 스캔 작업이 즉시 중단되도록 함
            AutoConnectAsync(_cts.Token).Forget();
        }

        private void Update()
        {
            // 스레드-세이프 큐에 쌓인 입력 데이터를 메인 스레드에서 꺼내어 이벤트로 발송
            while (_inputQueue.TryDequeue(out (int padNumber, bool isDown) result))
            {
                ProcessHardwareInput(result.padNumber, result.isDown);
            }
        }

        private void ProcessHardwareInput(int padNumber, bool isDown)
        {
            if (OnHardwareInput != null)
            {
                OnHardwareInput.Invoke(padNumber, isDown);
            }
        }

        private async UniTaskVoid AutoConnectAsync(CancellationToken token)
        {
            string[] portNames = SerialPort.GetPortNames();
            Debug.Log($"[ArduinoManager] 발견된 전체 COM 포트 수: {portNames.Length}");

            foreach (string portName in portNames)
            {
                // 이유: 작업 중 취소 요청이 들어오면 불필요한 포트 스캔을 즉시 중단하기 위함
                if (token.IsCancellationRequested) return;
                
                if (IsConnected) break;

                await TryConnectPortAsync(portName, token);
            }

            if (token.IsCancellationRequested) return;

            if (!IsConnected) 
            {
                Debug.LogWarning("[ArduinoManager] C2 아두이노 장치를 찾지 못했습니다.");
            }
            else
            {
                StartReadingThread();
            }
        }

        private async UniTask TryConnectPortAsync(string portName, CancellationToken token)
        {
            await UniTask.RunOnThreadPool(async () =>
            {
                if (token.IsCancellationRequested) return;

                SerialPort tempPort = new SerialPort(portName, 9600);
                tempPort.ReadTimeout = 2000;
                tempPort.DtrEnable = true;

                if (token.IsCancellationRequested)
                {
                    tempPort.Dispose();
                    return;
                }

                try
                {
                    tempPort.Open();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ArduinoManager] 포트 열기 실패 ({portName}): {e.Message}");
                    tempPort.Dispose(); 
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    tempPort.Close();
                    tempPort.Dispose();
                    return;
                }

                // 아두이노 재부팅 및 초기화 대기 시간
                // 이유: 연결 직후 아두이노 보드가 재부팅되므로 시리얼 통신 안정화를 위해 잠시 대기함
                bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(2.5f), cancellationToken: token).SuppressCancellationThrow();
                
                if (isCanceled || token.IsCancellationRequested)
                {
                    tempPort.Close();
                    tempPort.Dispose();
                    return;
                }

                string response = string.Empty;
                try
                {
                    if (tempPort.BytesToRead > 0)
                    {
                        response = tempPort.ReadExisting();
                    }
                }
                catch (TimeoutException)
                {
                    Debug.LogWarning($"[ArduinoManager] 응답 타임아웃 ({portName})");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ArduinoManager] 읽기 예외 ({portName}): {e.Message}");
                }

                await UniTask.SwitchToMainThread();

                if (token.IsCancellationRequested)
                {
                    tempPort.Close();
                    tempPort.Dispose();
                    return;
                }

                // 이유: GameConstants.Hardware 하위에 C2용 아두이노 식별 문자열이 있다고 가정하고 매칭 검사
                if (response.Contains("Arduino_C2") || response.Contains("Pad_Controller")) 
                {
                    tempPort.ReadTimeout = 10;
                    _arduinoPort = tempPort;
                    Debug.Log($"[ArduinoManager] C2 아두이노 연결 성공: {portName}");
                }
                else
                {
                    tempPort.Close();
                    tempPort.Dispose();
                }
            });
        }

        private void StartReadingThread()
        {
            if (_readThread == null || !_readThread.IsAlive)
            {
                _readThread = new Thread(ReadPortLoop);
                _readThread.IsBackground = true;
                _readThread.Start();
                Debug.Log("[ArduinoManager] 백그라운드 시리얼 수신 스레드 가동 시작");
            }
        }

        private void ReadPortLoop()
        {
            while (_isRunning)
            {
                if (IsConnected)
                {
                    try
                    {
                        if (_arduinoPort.BytesToRead > 0)
                        {
                            string inputLine = _arduinoPort.ReadLine().Trim();
                            
                            if (!string.IsNullOrEmpty(inputLine))
                            {
                                ParseAndEnqueueInput(inputLine);
                            }
                        }
                    }
                    catch (TimeoutException) { /* ReadTimeout 정상 패스 */ }
                    catch (Exception e)
                    {
                        DateTime now = DateTime.UtcNow;
                        if (now - _lastWarnTime > WarnThrottle)
                        {
                            _lastWarnTime = now;
                            string bytesInfo = "N/A";
                            try { bytesInfo = _arduinoPort.BytesToRead.ToString(); } catch { }

                            Debug.LogWarning($"[ArduinoManager] 아두이노 수신 예외: {e.Message} | BytesToRead: {bytesInfo}");
                        }
                    }
                }

                Thread.Sleep(10); // CPU 점유율 방지
            }
        }

        /// <summary>
        /// "1 On", "12 Off" 형식의 문자열을 분석하여 큐에 삽입합니다.
        /// </summary>
        private void ParseAndEnqueueInput(string rawInput)
        {
            string[] parts = rawInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out int padNumber))
                {
                    string stateStr = parts[1].Trim().ToLower();
                    if (stateStr == "on")
                    {
                        _inputQueue.Enqueue((padNumber, true));
                    }
                    else if (stateStr == "off")
                    {
                        _inputQueue.Enqueue((padNumber, false));
                    }
                    else
                    {
                        Debug.LogWarning($"[ArduinoManager] 알 수 없는 상태 값 수신: {rawInput}");
                    }
                }
            }
        }

        /// <summary> 아두이노로 명령을 전송합니다. (LED 제어 등 필요 시 사용) </summary>
        public void SendCommand(string command)
        {
            if (IsConnected)
            {
                try
                {
                    _arduinoPort.WriteLine(command);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ArduinoManager] 전송 오류: {e.Message}");
                }
            }
        }

        private void OnDestroy()
        {
            _isRunning = false;

            // 이유: 오브젝트가 파괴될 때 실행 중이던 비동기 스캔 작업을 즉각적으로 중단시켜 메모리 누수를 막기 위함
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_readThread != null && _readThread.IsAlive)
            {
                _readThread.Join(500); // 스레드 종료 대기
            }

            if (IsConnected)
            {
                _arduinoPort.Close();
                _arduinoPort.Dispose();
            }
        }
    }
}