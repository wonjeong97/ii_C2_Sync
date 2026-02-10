using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;

public class SerialRfidReader : MonoBehaviour
{
    [SerializeField] private string portName = "COM9";
    [SerializeField] private int baudRate = 9600;

    private SerialPort _serialPort;
    private Thread _readThread;
    private bool _isRunning = false;
    private string _latestData = "";

    void Start()
    {
        OpenConnection();
    }

    /// <summary>
    /// 시리얼 포트를 열고 수신용 스레드를 시작함.
    /// </summary>
    private void OpenConnection()
    {
        try
        {
            _serialPort = new SerialPort(portName, baudRate);
            _serialPort.ReadTimeout = 1000;
            _serialPort.Open();

            _isRunning = true;
            _readThread = new Thread(ReadSerialData);
            _readThread.Start();

            Debug.Log($"[Serial] {portName} 포트 연결 성공.");
        }
        catch (Exception e)
        {
            // TODO: 포트 점유 중이거나 장치 연결 해제 시 예외 처리 보강 필요
            Debug.LogError($"[Serial] 연결 실패: {e.Message}");
        }
    }

    /// <summary>
    /// 별도 스레드에서 데이터를 지속적으로 읽음.
    /// </summary>
    private void ReadSerialData()
    {
        while (_isRunning && _serialPort != null && _serialPort.IsOpen)
        {
            try
            {
                if (_serialPort.BytesToRead > 0)
                {
                    // RFID 태그 데이터 한 줄을 읽음
                    string data = _serialPort.ReadLine();
                    if (!string.IsNullOrEmpty(data))
                    {
                        _latestData = data.Trim();
                    }
                }
            }
            catch (TimeoutException) { /* 데이터 미수신 시 대기 */ }
            catch (Exception e)
            {
                Debug.LogWarning($"[Serial] 수신 오류: {e.Message}");
            }
        }
    }

    void Update()
    {
        // 수신된 데이터가 있으면 콘솔에 출력
        if (!string.IsNullOrEmpty(_latestData))
        {
            Debug.Log($"[RFID Tag ID]: {_latestData}");
            
            // 데이터 처리 후 변수 초기화 (중복 출력 방지)
            _latestData = ""; 
        }
    }

    void OnApplicationQuit()
    {
        // 어플리케이션 종료 시 안전하게 포트와 스레드 정리
        _isRunning = false;

        if (_readThread != null && _readThread.IsAlive)
            _readThread.Join();

        if (_serialPort != null && _serialPort.IsOpen)
        {
            _serialPort.Close();
            Debug.Log("[Serial] 포트 닫힘.");
        }
    }
}