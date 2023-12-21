using System;
using System.Collections;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

using UnityEngine;

namespace Utility
{
    /// <summary>
    /// NTP를 이용한 시간 체크 클레스
    /// Task를 용한 비동기 방식으로 체크
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        private WaitForSeconds ws_second;

        private Coroutine checkFullChargeCoroutine;
        private Coroutine checkDiceChargeCoroutine;

        private DateTime lastFullChargeUtcTime;
        private DateTime lastDiceChargeStartUtcTime;

        public Action<double> OnCountDiceCharging; // Event on every second for charge dice(remain time)
        public Action OnCompleteDiceCharging;

        private void Awake()
        {
            ws_second = new WaitForSeconds(1);
        }

        public void Init()
        {
            CurrencyData currencyData = ProgramManager.singleton.saveData.currencyData;

            lastFullChargeUtcTime = DateTime.Parse(currencyData.lastFullChargeUtcTime, new CultureInfo("en-US"));
            lastDiceChargeStartUtcTime = DateTime.Parse(currencyData.lastDiceChargeStartUtcTime, new CultureInfo("en-US"));
        }

        private void Start()
        {
            StartCheckFullCharge();
            StartCheckDiceCharge();
        }



        #region FullCharge
        private void StartCheckFullCharge()
        {
            checkFullChargeCoroutine = StartCoroutine(CheckFullCharge());
        }

        private IEnumerator CheckFullCharge()
        {
            while(true)
            {
                if (DateTime.UtcNow.Day > lastFullChargeUtcTime.Day)
                {
                    Task<bool> task = Task.Run(CheckServerTime);
                    
                    yield return new WaitUntil(() => task.IsCompleted);

                    if (task.Result)
                        ChargeFull();
                    else
                    {
                        StopCoroutine(checkFullChargeCoroutine);

                        UIManager.singleton.ShowNetworkErrorPopup(StartCheckFullCharge);
                    }
                }

                yield return ws_second;
            }
        }

        private void ChargeFull()
        {
            CurrencyData currencyData = ProgramManager.singleton.saveData.currencyData;

            lastFullChargeUtcTime = DateTime.UtcNow;

            currencyData.lastFullChargeUtcTime = lastFullChargeUtcTime.ToString(new CultureInfo("en-US"));

            currencyData.RefreshChargeItem();

            ProgramManager.singleton.SaveData();
        }
        #endregion



        #region Dice
        private void StartCheckDiceCharge()
        {
            checkDiceChargeCoroutine = StartCoroutine(CheckDiceCharge());
        }

        private IEnumerator CheckDiceCharge()
        {
            CurrencyData currencyData = ProgramManager.singleton.saveData.currencyData;
            bool isComplete = false;
            
            while (true)
            {
                if(currencyData.dice < CurrencyData.MAX_DICE_COUNT)
                {
                    isComplete = false;
                    TimeSpan timeSpan = DateTime.UtcNow - lastDiceChargeStartUtcTime;

                    if (timeSpan.TotalMinutes >= CurrencyData.DICE_CHARGE_INTERVAL)
                    {
                        Task<bool> task = Task.Run(CheckServerTime);

                        yield return new WaitUntil(() => task.IsCompleted);

                        if (task.Result)
                        {
                            ChargeDice();
                        }
                        else
                        {
                            StopCoroutine(checkDiceChargeCoroutine);

                            UIManager.singleton.ShowNetworkErrorPopup(StartCheckDiceCharge);
                        }
                    }
                    else
                    {
                        yield return ws_second;
                    }

                    double remain = CurrencyData.DICE_CHARGE_INTERVAL * 60 - timeSpan.TotalSeconds;
                    OnCountDiceCharging?.Invoke(remain);
                }
                else
                {
                    lastDiceChargeStartUtcTime = DateTime.UtcNow;

                    if(!isComplete)
                    {
                        isComplete = true;
                        OnCompleteDiceCharging?.Invoke();
                    }

                    yield return ws_second;
                }
            }
        }

        private void ChargeDice()
        {
            CurrencyData currencyData = ProgramManager.singleton.saveData.currencyData;

            TimeSpan timeSpan = DateTime.UtcNow - lastDiceChargeStartUtcTime;
            int chargingAmount = Mathf.FloorToInt((int)timeSpan.TotalMinutes / CurrencyData.DICE_CHARGE_INTERVAL);

            currencyData.AddDice(chargingAmount, true);

            lastDiceChargeStartUtcTime = lastDiceChargeStartUtcTime.AddMinutes(chargingAmount * CurrencyData.DICE_CHARGE_INTERVAL);
            currencyData.lastDiceChargeStartUtcTime = lastDiceChargeStartUtcTime.ToString(new CultureInfo("en-US"));

            ProgramManager.singleton.SaveData();
        }
        #endregion



        #region NTP
        public async void CheckServerTime(Action onSucessCallback, Action onFailedCallback)
        {
            DateTime serverUtcTime = await Task.Run(() => GetNetworkTime());
            //DateTimeOffset serverTime = BackendManager.singleton.GetServerTime();
            //DateTimeOffset serverTime = await Task.Run(() => BackendManager.singleton.GetServerTime());

            if (serverUtcTime != null)
            {
                TimeSpan timeSpan = DateTime.UtcNow - serverUtcTime;

                double timeGap = Math.Abs(timeSpan.TotalMinutes);

                // 10분 이상 차이가 생기면 비정상 로그 남김(확인 완료)
                if (timeGap < 10)
                {
                    onSucessCallback?.Invoke();
                }
                else
                {
                    //BackendLog.Instance.AbnormalLog($"서버 시간과 디바이스 시간의 차이가 남::{timeGap}분");
                    onFailedCallback?.Invoke();
                }
            }
            else
            {
                onFailedCallback?.Invoke();
            }
        }

        private bool CheckServerTime()
        {
            try
            {
                DateTime serverUtcNow = GetNetworkTime();

                TimeSpan timeSpan = DateTime.UtcNow - serverUtcNow;

                double timeGap = Math.Abs(timeSpan.TotalMinutes);

                if (timeGap < 1)
                    return true;
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }

        private DateTime GetNetworkTime()
        {
            //default Windows time server
            const string ntpServer = "time.windows.com";

            // NTP message size - 16 bytes of the digest (RFC 2030)
            var ntpData = new byte[48];

            //Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; //LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = Dns.GetHostEntry(ntpServer).AddressList;
            var ipv4Address = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
            var ipEndPoint = new IPEndPoint(ipv4Address, 123);
            //NTP uses UDP

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect(ipEndPoint);

                //Stops code hang if NTP is blocked
                socket.ReceiveTimeout = 10000;

                socket.Send(ntpData);
                socket.Receive(ntpData);
                socket.Close();
            }

            //Offset to get to the "Transmit Timestamp" field (time at which the reply 
            //departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;

            //Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);

            //Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            //Convert From big-endian to little-endian
            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            //**UTC** time
            var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);

            return networkDateTime;
        }

        // stackoverflow.com/a/3294698/162671
        uint SwapEndianness(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
                           ((x & 0x0000ff00) << 8) +
                           ((x & 0x00ff0000) >> 8) +
                           ((x & 0xff000000) >> 24));
        }
        #endregion



    }
}