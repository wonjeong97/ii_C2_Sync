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
            
            AutoConnectAsync(_cts.Token).Forget();
        }

        private void Update()
        {
            int processCount = 0;
            const int maxProcessPerFrame = 30; 

            if (_inputQueue.Count > 100)
            {
                Debug.LogWarning($"[ArduinoManager] 비정상적인 입력 폭주 감지 (현재 큐: {_inputQueue.Count}개). 오래된 큐를 강제 정리합니다.");
                
                while (_inputQueue.Count > 20)
                {
                    _inputQueue.TryDequeue(out _);
                }
            }

            while (processCount < maxProcessPerFrame && _inputQueue.TryDequeue(out (int padNumber, bool isDown) result))
            {
                ProcessHardwareInput(result.padNumber, result.isDown);
                processCount++;
            }
        }

        private void ProcessHardwareInput(int padNumber, bool isDown)
        {
            if (OnHardwareInput != null)
            {
                OnHardwareInput.Invoke(padNumber, isDown);
            }
        }

        /// <summary>
        /// 아두이노와의 직렬 연결을 강제로 끊고 다시 연결합니다.
        /// </summary>
        public void Reconnect()
        {
            Debug.Log("[ArduinoManager] 아두이노 재연결 및 하드웨어 리셋 시도...");
            
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

        private async UniTaskVoid AutoConnectAsync(CancellationToken token)
        {
            string[] portNames = SerialPort.GetPortNames();
            Debug.Log($"[ArduinoManager] 발견된 전체 COM 포트 수: {portNames.Length}");

            foreach (string portName in portNames)
            {
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

                if (response.Contains("Sensor")) 
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

                Thread.Sleep(10); 
            }
        }

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
                }
            }
        }

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