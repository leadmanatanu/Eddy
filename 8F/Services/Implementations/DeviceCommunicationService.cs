using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace _8F.Services
{
    public class DeviceCOM
    {
        public SerialPort port = new();
        public static List<ChannelData> channelDatas = new();
        public static List<Response> responses = new();
        public static bool IsResponseRefreshRequired = false;
        public static bool IsBalanceAll = false;
        public static bool IsBalanceBusyEnable = false;
        public static bool IsResponseClearRequired = false;
        public static int CommunicationType;
        public static string PortName = string.Empty;
        public static int BaudRate;
        public static string IpAddress = string.Empty;
        public static int SPort;
        public static int ChannelNo = 4;
        public static int DefaultHeight = 0;
        public static int DefaultWidth = 0;
        public static int DefaultHeight_O = 0;
        public static int DefaultWidth_O = 0;
        public static int DefaultAngel_O = 0;
        public static string ConnectionString = string.Empty;
        public static bool IsLogEnable = false;
        public static bool IsSystemBusy = false;
        public static DateTime busyStamp = System.DateTime.Now; 
        public static Part part = new();
        public static List<Counter> counter = new();
        public static bool IsLogDisable = false;
        public static bool IsBalanceRequired = false;
        public static bool IsBinRequired = false;
        public static int ERRCode = 0;
        public static string Code = string.Empty;
        public static bool IsJSON = false;
        public static bool IsLogRequiredOnBalance = false;
        public static bool IsAutoEllipseActive = false;
        DispatcherTimer dispatcherTimer = new();
        TcpClient? client;
        NetworkStream? stream;
        public static bool PortAck = false ;
        public static string PortData = "";
        private ManualResetEvent _ackEvent = new ManualResetEvent(false);

        public void InitialPort(int communicationType, string portName, int baudRate, string ipAddress, int sport)
        {
            CommunicationType = communicationType;
            PortName = portName;
            BaudRate = baudRate;
            IpAddress = ipAddress;
            SPort = sport;

            if (CommunicationType == 0)
            {
                port = new SerialPort
                {
                    BaudRate = BaudRate,
                    DataBits = 8,
                    Handshake = Handshake.None,
                    Parity = Parity.None,
                    PortName = portName,
                    StopBits = StopBits.One,
                    ReadTimeout = 500,
                    WriteTimeout = 2000,
                    DtrEnable = true,
                    RtsEnable = true,
                };
                port.ReceivedBytesThreshold = 1;
                port.DataReceived += serialPort_DataReceived;
            }
            else if (CommunicationType == 1)
            {
                dispatcherTimer = new DispatcherTimer();
                dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
                dispatcherTimer.Interval = new TimeSpan(10000000);
                dispatcherTimer.Start();

                client = new TcpClient();
            }

            if (counter == null)
            {
                counter = new List<Counter>();
                var cnt1 = new Counter();
                cnt1.Id = 1;
                counter.Add(cnt1);

                var cnt2 = new Counter();
                cnt2.Id = 2;
                counter.Add(cnt2);

                var cnt3 = new Counter();
                cnt3.Id = 3;
                counter.Add(cnt3);

                var cnt4 = new Counter();
                cnt4.Id = 4;
                counter.Add(cnt4);
            }

        }
        private void dispatcherTimer_Tick(object? sender, EventArgs e)
        {
            if (client == null) return;
            client.NoDelay = false;
            if (!client.Connected)
            {
                client = new TcpClient();
                IPAddress iPAddress = IPAddress.Parse(IpAddress);
                var ipEndPoint = new IPEndPoint(iPAddress, SPort);
                client.Connect(ipEndPoint);
            }
            if (client.Connected)
            {
                stream = client.GetStream();
                if (stream.DataAvailable)
                {
                    try
                    {
                        var buffer = new byte[client.Available];
                        int received = stream.Read(buffer);
                        var message = Encoding.UTF8.GetString(buffer, 0, received);
                        new Thread(() =>
                        {
                            ProcessPortData(message);
                        }).Start();

                    }
                    catch (Exception)
                    {

                    }

                }
            }
        }
        private void serialPort_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            try
            {
                System.Threading.Thread.Sleep(50);
                SerialPort sp = (SerialPort)sender;
                short[] data;
                string indata = string.Empty;
                
                if (PortAck)
                {
                    indata = sp.ReadExisting();
                    PortData = indata;
                    PortAck = false;
                    _ackEvent.Set();  // signal that data is received
                }
                else
                {
                    if (IsJSON)
                    {
                        indata = sp.ReadExisting();
                        data = new short[indata.Length];
                    }
                    else
                    {
                        byte[] buffer = new byte[sp.BytesToRead];
                        sp.Read(buffer, 0, buffer.Length);

                        data = buffer.Select(b => (short)b).ToArray();
                    }

                    new Thread(() =>
                    {
                        if(IsJSON)
                        {
                            ProcessPortData(indata);
                        }
                        else
                        {
                            ProcessPortDataBytpe(data);
                        }
                            
                        
                    }).Start();
                }

            }
            catch (Exception)
            {

            }
        }

        public static void Test()
        {

            short[] testData = new short[]
{
    0x02, 0x14, 0x32, 0x01, 0x00,

    0x01, 0x01, 0x00, 0x00, 0x00, 0x00,
    0x02, 0x01, 0x01, 0x00, 0xFF, 0xFF,
    0x03, 0x01, 0x02, 0x00, 0xFE, 0xFF,
    0x04, 0x01, 0x03, 0x00, 0xFD, 0xFF,
    0x05, 0x01, 0x04, 0x00, 0xFC, 0xFF,
    0x06, 0x01, 0x05, 0x00, 0xFB, 0xFF,
    0x07, 0x01, 0x06, 0x00, 0xFA, 0xFF,
    0x08, 0x01, 0x07, 0x00, 0xF9, 0xFF,

    0x38, 0x9A
};
                DeviceCOM deviceCOM = new DeviceCOM();
                deviceCOM.ProcessPortDataBytpe(testData);
        }

        private void ProcessPortDataBytpe(short[] indata)
        {
            try
            {
                if (indata.Length>1)
                {
                    

                    if (indata[1] == 19)
                    {
                        IsResponseClearRequired = true;
                    }
                    else if (indata[1] == 20)
                    {
                        //var res = JsonConvert.DeserializeObject<Response>(indata);
                        Response res = new Response();
                        res.CN = indata[3];
                        res.OR = indata[4];
                        res.FC = 20;
                        int length = indata[2]/6;
                        res.FD = new List<FreqResult>();
                        for (int i = 0; i < length; i++)
                        {
                            FreqResult fd = new FreqResult();
                            fd.FN = indata[5 + (i * 6)];
                            fd.R = indata[6 + (i * 6)];
                            int offset = 7 + (i * 6);
                            short x = (short)((ushort)indata[offset] | ((ushort)indata[offset + 1] << 8));
                            short y = (short)((ushort)indata[offset + 2] | ((ushort)indata[offset + 3] << 8));
                            fd.X = x;
                            fd.Y = y;
                            res.FD.Add(fd);
                        }

                        if (res != null && ChannelNo >= res.CN)
                        {
                            if (IsAutoEllipseActive)
                            {
                                res.IsAutoEllipseTest = true;
                            }
                            responses.Add(res);

                            if (!IsAutoEllipseActive)
                            {
                                List<int> targetIds = new List<int> { res.CN };
                                if (res.CN == 0)
                                {
                                    for (int ch = 1; ch <= ChannelNo; ch++)
                                    {
                                        targetIds.Add(ch);
                                    }
                                }
                                else
                                {
                                    targetIds.Add(0);
                                }

                                foreach (int targetId in targetIds)
                                {
                                    var cnt = counter.FirstOrDefault(c => c.Id == targetId);
                                    if (cnt == null)
                                    {
                                        cnt = new Counter { Id = targetId };
                                        counter.Add(cnt);
                                    }

                                    if (res.OR == 1)
                                    {
                                        cnt.ResultOkCount = cnt.ResultOkCount + 1;
                                    }
                                    else
                                    {
                                        cnt.ResultOkNotCount = cnt.ResultOkNotCount + 1;
                                    }
                                    cnt.ResultCount = cnt.ResultOkCount + cnt.ResultOkNotCount;
                                }
                                IsResponseRefreshRequired = true;

                                if (!string.IsNullOrEmpty(Code))
                                {
                                    Task.Run(() =>
                                    {
                                        WriteLogCSV(Convert.ToBoolean(res.OR), DateTime.Now, res);
                                    });
                                }

                                if (!IsLogDisable && IsLogEnable)
                                {
                                    Task.Run(() =>
                                    {
                                        WriteLog(res.CN, Convert.ToBoolean(res.OR), DateTime.Now, res);
                                    });
                                }

                                DeviceCOM.IsLogDisable = false;
                            }

                        }
                    }
                    else if (indata[1] == 21)
                    {
                        IsSystemBusy = true;
                        busyStamp = System.DateTime.Now;
                    }
                    else if (indata[1] == 22)
                    {
                        IsSystemBusy = false;                        
                        if (indata[3] == 0)
                        {                            
                            if (IsBalanceBusyEnable)
                            {
                                IsResponseClearRequired = true;
                                IsBalanceBusyEnable = false;
                            }
                        }
                        else
                        {
                            ERRCode = indata[4];
                        }
                    }
                }
                else
                {

                }
            }
            catch (Exception)
            {

            }
        }

        private void ProcessPortData(string indata)
        {
            try
            {
                if (!string.IsNullOrEmpty(indata))
                {
                    var res = JsonConvert.DeserializeObject<Response>(indata);

                    if (res != null && res.FC == 19)
                    {
                        IsResponseClearRequired = true;
                    }
                    else if (res != null && res.FC == 20)
                    {
                        if (res != null && ChannelNo >= res.CN)
                        {
                            if (IsAutoEllipseActive)
                            {
                                res.IsAutoEllipseTest = true;
                            }
                            responses.Add(res);

                            if (!IsAutoEllipseActive)
                            {
                                List<int> targetIds = new List<int> { res.CN };
                                if (res.CN == 0)
                                {
                                    for (int ch = 1; ch <= ChannelNo; ch++)
                                    {
                                        targetIds.Add(ch);
                                    }
                                }
                                else
                                {
                                    targetIds.Add(0);
                                }

                                foreach (int targetId in targetIds)
                                {
                                    var cnt = counter.FirstOrDefault(c => c.Id == targetId);
                                    if (cnt == null)
                                    {
                                        cnt = new Counter { Id = targetId };
                                        counter.Add(cnt);
                                    }

                                    if (res.OR == 1)
                                    {
                                        cnt.ResultOkCount = cnt.ResultOkCount + 1;
                                    }
                                    else
                                    {
                                        cnt.ResultOkNotCount = cnt.ResultOkNotCount + 1;
                                    }
                                    cnt.ResultCount = cnt.ResultOkCount + cnt.ResultOkNotCount;
                                }
                                IsResponseRefreshRequired = true;

                                if (!string.IsNullOrEmpty(Code))
                                {
                                    Task.Run(() =>
                                    {
                                        WriteLogCSV(Convert.ToBoolean(res.OR), DateTime.Now, res);
                                    });
                                }

                                if (!IsLogDisable && IsLogEnable)
                                {
                                    Task.Run(() =>
                                    {
                                        WriteLog(res.CN, Convert.ToBoolean(res.OR), DateTime.Now, res);                                    
                                    });
                                }

                                DeviceCOM.IsLogDisable = false;
                            }

                        }
                    }
                    else if (res?.FC == 21)
                    {
                        IsSystemBusy = true;
                        busyStamp = System.DateTime.Now;
                    }
                    else if (res?.FC == 22)
                    {
                        IsSystemBusy = false;
                        ERRCode = res.ERR;
                        if (res.ERR != 16 & res.ERR != 17)
                        {
                            if (IsBalanceBusyEnable)
                            {
                                IsResponseClearRequired = true;
                                IsBalanceBusyEnable = false;
                            }
                        }
                    }
                }
                else
                {

                }
            }
            catch (Exception)
            {

            }
        }

        public void WriteLog(int ChId, bool Result, DateTime TimeStamp, Response res)
        {
            try
            {
                if (res?.FD == null)
                    return;

                var targetCh = DeviceCOM.channelDatas.FirstOrDefault(r => r.Id == ChId);
                if (targetCh == null) return;

                var enabledChannels = targetCh.graphDatas.Where(g => g.isEnable).ToList();
                var enabledFns = enabledChannels.Select(g => g.Id).ToHashSet();

                // Validate that all enabled frequency numbers are present before saving
                var uniqueFns = res.FD.Where(f => f != null).Select(f => f.FN).Distinct().ToList();
                if (enabledFns.Count > 0)
                {
                    if (!enabledFns.All(fn => uniqueFns.Contains(fn)))
                    {
                        // Incomplete data for enabled channels - do not fabricate or save
                        return;
                    }
                }
                else
                {
                    if (uniqueFns.Count != 8 || !Enumerable.Range(1, 8).All(fn => uniqueFns.Contains(fn)))
                        return;
                }

                using (var con = new NpgsqlConnection(ConnectionString))
                {
                    con.Open();
                    var fdData = JsonConvert.SerializeObject(targetCh.graphDatas);

                    var partData = JsonConvert.SerializeObject(DeviceCOM.part);

                    // Persist ONLY the ON (enabled) channels in FrequencyData
                    var frequencyList = res.FD
                        .Where(f => f != null && (enabledFns.Count == 0 || enabledFns.Contains(f.FN)))
                        .OrderBy(f => f.FN)
                        .ToList();
                    var frequencyDataJson = JsonConvert.SerializeObject(frequencyList);

                    if (ChId == 1)
                    {
                        const string sql = @"
                            INSERT INTO public.""Logs"" (
                                ""ChId"", ""Result"", ""FDData"", ""PartData"", ""PartName"", 
                                ""BatchName"", ""SrNo"", ""BatchNo"", ""TimeStamp"", ""FrequencyData"", ""ResultData""
                            ) VALUES (
                                @ChId, @Result, @FDData::json, @PartData::json, @PartName, 
                                @BatchName, @SrNo, @BatchNo, @TimeStamp, @FrequencyData::jsonb, @ResultData::jsonb
                            );
                            SELECT count(1) FROM public.""Logs"" WHERE ""BatchName"" = @BatchName AND ""BatchNo"" = @BatchNo;";

                        using (var cmd = new NpgsqlCommand(sql, con))
                        {
                            cmd.Parameters.AddWithValue("@ChId", ChId);
                            cmd.Parameters.AddWithValue("@Result", Result);
                            cmd.Parameters.AddWithValue("@FDData", fdData ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@PartData", partData ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@PartName", (object?)DeviceCOM.part?.Name ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BatchName", (object?)DeviceCOM.part?.BatchName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@SrNo", (object?)Code ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@BatchNo", (object?)DeviceCOM.part?.BatchNo ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@TimeStamp", TimeStamp);
                            cmd.Parameters.AddWithValue("@FrequencyData", frequencyDataJson);
                            cmd.Parameters.AddWithValue("@ResultData", frequencyDataJson);

                            var count = cmd.ExecuteScalar();

                            if (DeviceCOM.part != null && DeviceCOM.part.BatchType == 1)
                            {
                                if (Convert.ToInt32(count) == DeviceCOM.part.BatchSize)
                                {
                                    // stop the logging 
                                }

                                DeviceCOM.part.BatchNo = DeviceCOM.part.BatchNo + 1;
                            }
                        }
                    }
                    else
                    {
                        if (!Result)
                        {
                            const string sql = "UPDATE public.\"Logs\" SET \"Result\" = 'false' WHERE \"Id\" = (SELECT max(\"Id\") FROM public.\"Logs\"); SELECT 1;";
                            using (var cmd = new NpgsqlCommand(sql, con))
                            {
                                var count = cmd.ExecuteScalar();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {

            }

            //Code = string.Empty;
        }

        private static void WriteLogCSV(bool Result, DateTime TimeStamp, Response res)
        {
            try
            {
                // Write to CSV File
                var ch = DeviceCOM.channelDatas.FirstOrDefault(c => c.Id == res.CN);
                if (ch != null)
                {
                    List<string> lines = new List<string>();
                    var FileName = "EddyLog_" + System.DateTime.Now.ToShortDateString();
                    string FilePath = System.Configuration.ConfigurationManager.AppSettings["CSVPath"]?.ToString() + FileName + ".csv";
                    if (!File.Exists(FilePath))
                    {
                        string line = "TimeStamp,Code,Operator Name,Result";
                        foreach (var fd in ch.graphDatas)
                        {
                            line = line + ",Frequency Result_" + fd.Id.ToString() + ",Frequency_" + fd.Id.ToString() + ",X_" + fd.Id.ToString() + ",Y_" + fd.Id.ToString();
                        }
                        lines.Add(line);
                    }

                    string data = System.DateTime.Now.ToString() + ","+ Code.Replace("\n", "").Replace("\r","") + "," + DeviceCOM.part.CheckedBy + "," + (Result == true ? "Ok" : "No Ok");

                    foreach (var fd in res.FD)
                    {
                        var Gdata = ch.graphDatas.FirstOrDefault(d => d.Id == fd.FN);
                        if (Gdata != null)
                        {
                            data = data + "," + (fd.R == 1 ? "Ok" : "No Ok") + "," + Gdata.freq.ToString() + "," + fd.X + "," + fd.Y;
                        }
                    }

                    lines.Add(data);

                    if (lines.Count > 0)
                    {
                        File.AppendAllLines(FilePath, lines);
                    }
                }
            }
            catch (Exception)
            {

            }
        }

        public bool WriteDataInBytes(byte[] data, bool isFrombak = false)
        {

            try
            {
                IsBalanceRequired = false;
                if (CommunicationType == 0)
                {
                    if (isFrombak)
                    {
                        if (port.IsOpen)
                        {
                            port.Close();
                        }
                    }

                    if (!port.IsOpen)
                    {
                        port.Open();
                        if (isFrombak)
                        {
                            port.DtrEnable = true;
                            port.RtsEnable = true;
                            // Optional: short delay to let device recognize the signals
                            Thread.Sleep(10);
                        }
                    }

                    //this.port.ReadExisting();
                    PortAck = true;
                    PortData = "";

                    ushort crc = ComputeCRC(data, 2);

                    byte crcLow = (byte)(crc & 0xFF);
                    byte crcHigh = (byte)(crc >> 8);

                    data[data.Length - 2] = crcLow; 
                    data[data.Length - 1] = crcHigh; 

                    this.port.Write(data, 0 , data.Length);

                    if (isFrombak)
                    {
                        bool received = _ackEvent.WaitOne(1000);

                        if (!received)
                        {
                            this.port.Write(data, 0, data.Length);
                        }

                        received = _ackEvent.WaitOne(1000);

                        if (!received)
                        {
                            this.port.Write(data, 0, data.Length);
                        }

                        if (!received)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        System.DateTime dateTime = DateTime.Now;
                        while (PortAck)
                        {

                            if ((DateTime.Now - dateTime).TotalMilliseconds > 500)
                            {
                                PortAck = false;
                            }
                        }
                    }

                    byte[] result = new byte[1];
                    result[0] = Convert.ToByte(PortData[0]);

                    if (result[0] == '0' || result[0] == '2' || result[0] == '3' || result[0] == '4')
                    {
                        if (result[0] == '2')
                        {
                            DeviceCOM.IsSystemBusy = true;
                            busyStamp = System.DateTime.Now;
                        }
                        else if (result[0] == '3')
                        {
                            IsBalanceRequired = true;
                        }
                        else if (result[0] == '4')
                        {
                            IsBinRequired = true;
                        }
                        return true;
                    }

                    return false;
                }                
                return false;
            }
            catch (Exception)
            {
                if (CommunicationType == 1)
                {
                    dispatcherTimer.Start();
                }

                return false;
            }
        }

        public bool WriteData(string data, bool isFrombak = false)
        {
            try
            {
                IsBalanceRequired = false;
                if (CommunicationType == 0)
                {
                    if (isFrombak)
                    {
                        if (port.IsOpen)
                        {
                            port.Close();
                        }
                    }

                    if (!port.IsOpen)
                    {
                        port.Open();
                        if (isFrombak)
                        {
                            port.DtrEnable = true;
                            port.RtsEnable = true;
                            // Optional: short delay to let device recognize the signals
                            Thread.Sleep(10);
                        }
                    }

                    //this.port.ReadExisting();
                    PortAck = true;
                    PortData = "";                    
                    this.port.Write(data);

                    if (isFrombak)
                    {
                        bool received = _ackEvent.WaitOne(1000);

                        if (!received)
                        {
                            this.port.Write(data);
                        }

                        received = _ackEvent.WaitOne(1000);

                        if (!received)
                        {
                            this.port.Write(data);
                        }

                        if (!received)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        System.DateTime dateTime = DateTime.Now;
                        while (PortAck)
                        {

                            if ((DateTime.Now - dateTime).TotalMilliseconds > 500)
                            {
                                PortAck = false;
                            }
                        }
                    }
                    
                    byte[] result = new byte[1];
                    result[0] = Convert.ToByte(PortData[0]);

                    if (result[0] == '0' || result[0] == '2' || result[0] == '3' || result[0] == '4')
                    {
                        if (result[0] == '2')
                        {
                            DeviceCOM.IsSystemBusy = true;
                            busyStamp = System.DateTime.Now;
                        }
                        else if (result[0] == '3')
                        {
                            IsBalanceRequired = true;
                        }
                        else if (result[0] == '4')
                        {
                            IsBinRequired = true;
                        }
                        return true;
                    }

                    return false;
                }
                else if (CommunicationType == 1)
                {
                    dispatcherTimer?.Stop();
                    if (client != null && !client.Connected)
                    {
                        client = new TcpClient();
                        IPAddress iPAddress = IPAddress.Parse(IpAddress);
                        var ipEndPoint = new IPEndPoint(iPAddress, SPort);
                        client.Connect(ipEndPoint);
                    }

                    if (client?.Connected == true)
                    {
                        var messageBytes = Encoding.UTF8.GetBytes(data);
                        stream = client.GetStream();
                        stream.Write(messageBytes, 0, messageBytes.Length);
                        stream = client.GetStream();
                        var buffer = new byte[client.Available];
                        int received = stream.Read(buffer);
                        stream.Flush();
                        if (buffer.Length > 0)
                        {
                            if (buffer[0] == '0' || buffer[0] == '2')
                            {
                                if (buffer[0] == '2')
                                {
                                    DeviceCOM.IsSystemBusy = true;
                                    busyStamp = System.DateTime.Now;
                                }
                                return true;
                            }
                        }
                        dispatcherTimer?.Start();
                        return true;
                    }
                    dispatcherTimer?.Start();
                    return false;
                }
                return false;
            }
            catch (Exception)
            {
                if (CommunicationType == 1)
                {
                    dispatcherTimer.Start();
                }

                return false;
            }
        }

        public bool GetSystemStatus(string data)
        {
            try
            {
                if (CommunicationType == 0)
                {
                    if (port.IsOpen)
                    {
                        port.Close();
                    }

                    //if (port.IsOpen)
                    //{
                    //    port.Close();
                    //}

                    //InitialPort(CommunicationType, PortName, BaudRate, IpAddress, SPort);

                    if (!port.IsOpen)
                    {
                        port.Open();
                    }
                    //this.port.ReadExisting();
                    PortAck = true;
                    PortData = "";
                    System.DateTime dateTime = DateTime.Now;
                    this.port.Write(data);

                    while (PortAck)
                    {

                        if ((DateTime.Now - dateTime).TotalMilliseconds > 200)
                        {
                            PortAck = false;
                        }
                    }

                    byte[] result = new byte[1];
                    result[0] = Convert.ToByte(PortData[0]);

                    if (result[0] == 21)
                    {
                        DeviceCOM.IsSystemBusy = true;
                        busyStamp = System.DateTime.Now;
                    }
                }
                else if (CommunicationType == 1)
                {
                    dispatcherTimer?.Stop();
                    if (client != null && !client.Connected)
                    {
                        client = new TcpClient();
                        IPAddress iPAddress = IPAddress.Parse(IpAddress);
                        var ipEndPoint = new IPEndPoint(iPAddress, SPort);
                        client.Connect(ipEndPoint);
                    }

                    if (client?.Connected == true)
                    {
                        var messageBytes = Encoding.UTF8.GetBytes(data);
                        stream = client.GetStream();
                        stream.Write(messageBytes, 0, messageBytes.Length);
                        stream = client.GetStream();
                        var buffer = new byte[client.Available];
                        int received = stream.Read(buffer);
                        stream.Flush();
                        dispatcherTimer?.Start();
                        if (buffer[0] == 21)
                        {
                            DeviceCOM.IsSystemBusy = true;
                        }
                    }
                    dispatcherTimer?.Start();
                }
                return true;
            }
            catch (Exception)
            {
                if (CommunicationType == 1)
                {
                    dispatcherTimer?.Start();
                }

                return false;
            }
        }

        public bool GetSystemStatusInBytes(byte[] data)
        {
            try
            {
                if (CommunicationType == 0)
                {
                    if (port.IsOpen)
                    {
                        port.Close();
                    }

                    //if (port.IsOpen)
                    //{
                    //    port.Close();
                    //}

                    //InitialPort(CommunicationType, PortName, BaudRate, IpAddress, SPort);

                    if (!port.IsOpen)
                    {
                        port.Open();
                    }
                    //this.port.ReadExisting();
                    PortAck = true;
                    PortData = "";
                    System.DateTime dateTime = DateTime.Now;

                    ushort crc = ComputeCRC(data, 2);

                    byte crcLow = (byte)(crc & 0xFF);
                    byte crcHigh = (byte)(crc >> 8);

                    data[data.Length - 2] = crcLow;
                    data[data.Length - 1] = crcHigh;

                    this.port.Write(data, 0, data.Length);

                    while (PortAck)
                    {

                        if ((DateTime.Now - dateTime).TotalMilliseconds > 200)
                        {
                            PortAck = false;
                        }
                    }

                    byte[] result = new byte[1];
                    result[0] = Convert.ToByte(PortData[1]);

                    if (result[0] == 21)
                    {
                        DeviceCOM.IsSystemBusy = true;
                        busyStamp = System.DateTime.Now;
                    }
                }              
                return true;
            }
            catch (Exception)
            {
                if (CommunicationType == 1)
                {
                    dispatcherTimer.Start();
                }

                return false;
            }
        }

        public GetSerialNumber GetSeialNumber()
        {
            SetSerialNumber setSerialNumber = new SetSerialNumber { FC = 31, S1 = 1234, S2 = 5678};
            string data = JsonConvert.SerializeObject(setSerialNumber);
            GetSerialNumber getSerialNumber = new GetSerialNumber(); 
            try
            {
                if (port.IsOpen)
                {
                    port.Close();
                }

                InitialPort(CommunicationType, PortName, BaudRate, IpAddress, SPort);

                if (!port.IsOpen)
                {
                    port.Open();
                }
                this.port.ReadExisting();
                this.port.Write(data);
                Thread.Sleep(100);
                string sData = this.port.ReadExisting();

                if (port.IsOpen)
                {
                    port.Close();
                }

                port.DataReceived += serialPort_DataReceived;
                if (!port.IsOpen)
                {
                    port.Open();
                }
                
                if (!string.IsNullOrEmpty(sData))
                {
                    var parsed = JsonConvert.DeserializeObject<GetSerialNumber>(sData);
                    if (parsed != null)
                    {
                        getSerialNumber = parsed;
                        getSerialNumber.S1 = setSerialNumber.S1;
                        getSerialNumber.S2 = setSerialNumber.S2;
                    }
                }
            }
            catch (Exception)
            {
                
            }

            return getSerialNumber;
        }

        public static ushort ComputeCRC(byte[] data, int length)
        {
            ushort crc = 0xFFFF;

            for (int pos = 0; pos < length; pos++)
            {
                crc ^= data[pos];

                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            return crc;
        }

    }

}
