using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using VContainer;
using ZLogger;

namespace My.Scripts.Hardware
{
    public class ArduinoManager : MonoBehaviour
    {
        public event Action<int, bool> OnHardwareInput;

        private SerialPort _arduinoPort;
        private readonly ConcurrentQueue<(int padNumber, bool isDown)> _inputQueue = new();
        private Thread _readThread;
        private bool _isRunning ;
        private CancellationTokenSource _cts;

        private DateTime _lastWarnTime = DateTime.MinValue;
        private readonly TimeSpan WarnThrottle = TimeSpan.FromSeconds(5);

        private ILogger<ArduinoManager> _logger;

        [Inject]
        public void Construct(ILogger<ArduinoManager> logger)
        {
            _logger = logger;
        }

        public bool IsConnected => _arduinoPort != null && _arduinoPort.IsOpen;

        private void Start()
        {
            _isRunning = true;
            _cts = new CancellationTokenSource();
            AutoConnectAsync(_cts.Token).Forget();
        }

        private void Update()
        {
            if (_inputQueue.Count > 100)
            {
                _logger.ZLogWarning($"입력 폭주 감지 (현재:{_inputQueue.Count}개). 큐 정리.");
                while (_inputQueue.Count > 20) _inputQueue.TryDequeue(out _);
            }

            int count = 0;
            while (count < 30 && _inputQueue.TryDequeue(out var result))
            {
                OnHardwareInput?.Invoke(result.padNumber, result.isDown);
                count++;
            }
        }

        public void Reconnect()
        {
            _logger.ZLogInformation($"아두이노 재연결 및 하드웨어 리셋 시도.");

            _isRunning = false;
            CancelCts();

            if (_readThread != null && _readThread.IsAlive) _readThread.Join(500);

            if (_arduinoPort != null)
            {
                try
                {
                    if (_arduinoPort.IsOpen) _arduinoPort.Close();
                }
                catch
                {
                }

                try
                {
                    _arduinoPort.Dispose();
                }
                catch
                {
                }

                _arduinoPort = null;
            }

            _isRunning = true;
            _cts = new CancellationTokenSource();
            AutoConnectAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid AutoConnectAsync(CancellationToken token)
        {
            string[] portNames = SerialPort.GetPortNames();
            foreach (string portName in portNames)
            {
                if (token.IsCancellationRequested || IsConnected) break;

                await TryConnectPortAsync(portName, token);
            }

            if (IsConnected) StartReadingThread();
            else _logger.ZLogWarning($"연결 가능한 아두이노 장치를 찾지 못함.");
        }

        private async UniTask TryConnectPortAsync(string portName, CancellationToken token)
        {
            // 수정: Func<UniTask> 캐스팅 유지 및 로그 문법 표준화
            await UniTask.RunOnThreadPool((Func<UniTask>)(async () =>
            {
                var tempPort = new SerialPort(portName, 9600) { ReadTimeout = 2000, DtrEnable = true };
                try
                {
                    tempPort.Open();
                    await UniTask.Delay(TimeSpan.FromSeconds(2.5f), cancellationToken: token)
                        .SuppressCancellationThrow();

                    if (token.IsCancellationRequested) return;

                    if (tempPort.BytesToRead > 0 && tempPort.ReadExisting().Contains("Sensor"))
                    {
                        tempPort.ReadTimeout = 10;
                        _arduinoPort = tempPort;
                        _logger.ZLogInformation($"아두이노 연결 성공: {portName}");
                    }
                    else
                    {
                        tempPort.Close();
                        tempPort.Dispose();
                    }
                }
                catch (Exception e)
                {
                    _logger.ZLogWarning(e, $"포트 연결 실패 ({portName})");
                    tempPort.Dispose();
                }
            }));
        }

        private void StartReadingThread()
        {
            if (_readThread == null || !_readThread.IsAlive)
            {
                _readThread = new Thread(ReadPortLoop) { IsBackground = true };
                _readThread.Start();
                _logger.ZLogInformation($"백그라운드 수신 스레드 가동.");
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
                            string inputLine = _arduinoPort.ReadLine()?.Trim();
                            if (!string.IsNullOrEmpty(inputLine)) ParseAndEnqueueInput(inputLine);
                        }
                    }
                    catch (TimeoutException)
                    {
                    }
                    catch (Exception e)
                    {
                        DateTime now = DateTime.UtcNow;
                        if (now - _lastWarnTime > WarnThrottle)
                        {
                            _lastWarnTime = now;
                            _logger.ZLogWarning($"아두이노 수신 예외: {e.Message}");
                        }
                    }
                }

                Thread.Sleep(10);
            }
        }

        private void ParseAndEnqueueInput(string rawInput)
        {
            string[] parts = rawInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out int padNumber))
            {
                bool isDown = parts[1].Trim().ToLower() == "on";
                _inputQueue.Enqueue((padNumber, isDown));
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
                    _logger.ZLogError(e, $"아두이노 명령 전송 실패");
                }
            }
        }

        private void OnDestroy()
        {
            _isRunning = false;
            CancelCts();
            if (_readThread != null && _readThread.IsAlive) _readThread.Join(500);
            if (IsConnected)
            {
                _arduinoPort.Close();
                _arduinoPort.Dispose();
            }
        }

        private void CancelCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}