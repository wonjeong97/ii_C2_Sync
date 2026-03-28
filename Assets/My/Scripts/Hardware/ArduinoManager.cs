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
    /// 하드웨어 발판 센서와 직렬 통신(Serial)을 수행하고 데이터를 처리하는 매니저 클래스.
    /// </summary>
    public class ArduinoManager : MonoBehaviour
    {
        public static ArduinoManager Instance;

        public Action<int, bool> OnHardwareInput;

        private SerialPort _arduinoPort;

        private ConcurrentQueue<(int padNumber, bool isDown)> _inputQueue =
            new ConcurrentQueue<(int padNumber, bool isDown)>();

        private Thread _readThread;
        private bool _isRunning = false;
        
        private CancellationTokenSource _cts;

        private DateTime _lastWarnTime = DateTime.MinValue;
        private readonly TimeSpan WarnThrottle = TimeSpan.FromSeconds(5);

        public bool IsConnected => _arduinoPort != null && _arduinoPort.IsOpen;

        /// <summary>
        /// 싱글톤 인스턴스 초기화 및 중복 파괴 처리.
        /// </summary>
        private void Awake()
        {
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                // 이유: 전역 시스템이므로 중복 생성을 엄격히 방지함.
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 초기 구동 시 포트 자동 검색 및 연결 시도.
        /// </summary>
        private void Start()
        {
            _isRunning = true;
            _cts = new CancellationTokenSource();
            
            AutoConnectAsync(_cts.Token).Forget();
        }

        /// <summary>
        /// 메인 스레드에서 스레드 안전 큐를 소모하여 이벤트를 전파함.
        /// </summary>
        private void Update()
        {
            int processCount = 0;
            const int maxProcessPerFrame = 30; 

            // 이유: 하드웨어 오류로 인한 무한 입력 발생 시 메모리 점유 및 게임 지연 방지.
            if (_inputQueue.Count > 100)
            {
                Debug.LogWarning($"[ArduinoManager] 비정상적인 입력 폭주 감지(현재:{_inputQueue.Count}개). 큐 강제 정리.");
                
                while (_inputQueue.Count > 20)
                {
                    _inputQueue.TryDequeue(out _);
                }
            }

            // 이유: 프레임당 소모량을 제한하여 메인 스레드 부하를 분산함.
            while (processCount < maxProcessPerFrame && _inputQueue.TryDequeue(out (int padNumber, bool isDown) result))
            {
                ProcessHardwareInput(result.padNumber, result.isDown);
                processCount++;
            }
        }

        /// <summary>
        /// 수신된 데이터를 외부 시스템에 전달함.
        /// </summary>
        /// <param name="padNumber">발판 번호</param>
        /// <param name="isDown">눌림 여부</param>
        private void ProcessHardwareInput(int padNumber, bool isDown)
        {
            if (OnHardwareInput != null)
            {
                OnHardwareInput.Invoke(padNumber, isDown);
            }
        }

        /// <summary>
        /// 기존 연결을 안전하게 해제하고 포트를 재탐색함.
        /// </summary>
        public void Reconnect()
        {
            Debug.Log("[ArduinoManager] 아두이노 재연결 및 하드웨어 리셋 시도.");
            
            _isRunning = false;

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            // 이유: 실행 중인 스레드가 완전히 멈출 때까지 대기하여 포트 점유 충돌 방지.
            if (_readThread != null && _readThread.IsAlive)
            {
                _readThread.Join(500); 
            }

            if (_arduinoPort != null)
            {
                try { if (_arduinoPort.IsOpen) _arduinoPort.Close(); } catch { }
                try { _arduinoPort.Dispose(); } catch { }
                _arduinoPort = null;
            }

            while (_inputQueue.TryDequeue(out _)) { }

            _isRunning = true;
            _cts = new CancellationTokenSource();
            AutoConnectAsync(_cts.Token).Forget();
        }

        /// <summary>
        /// 시스템의 모든 COM 포트를 순회하며 유효한 장치를 검색함.
        /// </summary>
        /// <param name="token">취소 토큰</param>
        /// <returns>비동기 작업</returns>
        private async UniTaskVoid AutoConnectAsync(CancellationToken token)
        {
            string[] portNames = SerialPort.GetPortNames();
            Debug.Log($"발견된 COM 포트 수: {portNames.Length}");

            foreach (string portName in portNames)
            {
                if (token.IsCancellationRequested) return;
                if (IsConnected) break;

                await TryConnectPortAsync(portName, token);
            }

            if (token.IsCancellationRequested) return;

            if (!IsConnected) 
            {
                Debug.LogWarning("연결 가능한 아두이노 장치를 찾지 못함.");
            }
            else
            {
                StartReadingThread();
            }
        }

        /// <summary>
        /// 단일 포트에 접속하여 핸드셰이크(Sensor 문자열 확인)를 시도함.
        /// </summary>
        /// <param name="portName">포트명</param>
        /// <param name="token">취소 토큰</param>
        /// <returns>비동기 작업</returns>
        private async UniTask TryConnectPortAsync(string portName, CancellationToken token)
        {
            await UniTask.RunOnThreadPool(async () =>
            {
                if (token.IsCancellationRequested) return;

                SerialPort tempPort = new SerialPort(portName, 9600);
                tempPort.ReadTimeout = 2000;
                // 이유: 아두이노 연결 시 자동 리셋을 유도하여 초기 상태 동기화.
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
                    Debug.LogWarning($"포트 열기 실패 ({portName}): {e.Message}");
                    tempPort.Dispose(); 
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    tempPort.Close();
                    tempPort.Dispose();
                    return;
                }

                // 이유: 아두이노 리셋 후 부팅 대기 및 시리얼 데이터 버퍼가 차오를 때까지 기다림.
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
                catch (Exception e)
                {
                    Debug.LogWarning($"식별 데이터 읽기 실패 ({portName}): {e.Message}");
                }

                await UniTask.SwitchToMainThread();

                if (token.IsCancellationRequested)
                {
                    tempPort.Close();
                    tempPort.Dispose();
                    return;
                }

                // 이유: 수신 데이터에 특정 식별자(Sensor)가 포함되어야 장치로 인정함.
                if (response.Contains("Sensor")) 
                {
                    tempPort.ReadTimeout = 10;
                    _arduinoPort = tempPort;
                    Debug.Log($"아두이노 연결 성공: {portName}");
                }
                else
                {
                    tempPort.Close();
                    tempPort.Dispose();
                }
            });
        }

        /// <summary>
        /// 시리얼 데이터를 지속적으로 읽어올 백그라운드 스레드를 생성함.
        /// </summary>
        private void StartReadingThread()
        {
            if (_readThread == null || !_readThread.IsAlive)
            {
                _readThread = new Thread(ReadPortLoop);
                _readThread.IsBackground = true;
                _readThread.Start();
                Debug.Log("백그라운드 수신 스레드 가동.");
            }
        }

        /// <summary>
        /// 백그라운드 스레드 루프: 실제 데이터를 읽어 파싱 함수로 전달함.
        /// </summary>
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
                    catch (TimeoutException) { /* 읽기 타임아웃은 의도된 동작이므로 무시 */ }
                    catch (Exception e)
                    {
                        // 이유: 연속적인 로그 기록으로 인한 과부하 방지 (5초 간격 제한).
                        DateTime now = DateTime.UtcNow;
                        if (now - _lastWarnTime > WarnThrottle)
                        {
                            _lastWarnTime = now;
                            Debug.LogWarning($"아두이노 수신 예외: {e.Message}");
                        }
                    }
                }

                // 이유: CPU 점유율 과다 사용 방지.
                Thread.Sleep(10); 
            }
        }

        /// <summary>
        /// 수신된 문자열 데이터를 분석하여 숫자와 상태값으로 분리 후 큐에 삽입함.
        /// </summary>
        /// <param name="rawInput">원본 데이터 문자열</param>
        private void ParseAndEnqueueInput(string rawInput)
        {
            // 예시 입력: "1 On" -> 결과값: (1, true)
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
                }
            }
        }

        /// <summary>
        /// 아두이노 장치로 명령 문자열을 전송함.
        /// </summary>
        /// <param name="command">전송할 명령</param>
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
                    Debug.LogError($"전송 실패: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 객체 파괴 시 모든 자원(포트, 스레드, 토큰)을 해제함.
        /// </summary>
        private void OnDestroy()
        {
            _isRunning = false;

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_readThread != null && _readThread.IsAlive)
            {
                _readThread.Join(500); 
            }

            if (IsConnected)
            {
                _arduinoPort.Close();
                _arduinoPort.Dispose();
            }
        }
    }
}