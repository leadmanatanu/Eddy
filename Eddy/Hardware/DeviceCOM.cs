using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

using System;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Net;
using System.Printing;
using System.Windows.Threading;
using System.Net.Sockets;
using System.IO;
using Npgsql;
using System.Diagnostics.Metrics;
using System.Windows.Media;
using System.Windows.Documents;
using OpenTK.Compute.OpenCL;
using SkiaSharp;



namespace Eddy
{
    public class DeviceCOM
    {
        public SerialPort port = new SerialPort();

        public static string PortName = string.Empty;
        public static string DBConnection = string.Empty;
        public static int BaudRate;
        public static int MaxValue;
        public static int MaxValueABS;
        public static int Factor;
        public static Configuration Configuration = null!;
        public static bool IsTubeSatart = false;
        public static GraphData graphData = new GraphData();
        public static readonly object AmpD1Lock = new();
        public static bool IsTestOn = false;
        public static bool IsAttRequired = false;
        public static bool IsJSON = false;
        public static DateTime busyStamp = System.DateTime.Now;
        public static Part? part;
        public static bool IsLogEnable = false;
        public static byte[]? receiveBytes;
        public static double[]? dataBuffer;
        public static bool IsCalibarationStart = false;

        public static int Ok = 0;
        public static int NoOk = 0;

        public static readonly bool EnablePerfLog =
            bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["EnablePerfLog"], out bool enabled) && enabled;

        private static long _pktCount = 0;
        private static long _s2Count = 0;
        private static long _s4Count = 0;
        private static long _s6Count = 0;
        private static long _sOtherCount = 0;
        private static readonly object _pktLogLock = new();

        public static void LogStep(string message)
        {
            if (!EnablePerfLog) return;

            string logMsg = $"[{DateTime.Now:HH:mm:ss.fff}] [STEP] {message}";
            System.Diagnostics.Trace.WriteLine(logMsg);
            lock (_pktLogLock)
            {
                try
                {
                    string logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "perf.log");
                    System.IO.File.AppendAllText(logPath, $"{logMsg}{Environment.NewLine}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PERF] LogStep exception: {ex.Message}");
                }
            }
        }

        public static void LogPacket(byte[] packet, string layer)
        {
            if (!EnablePerfLog || packet == null || packet.Length == 0) return;

            long total = System.Threading.Interlocked.Increment(ref _pktCount);
            int type = packet[0];
            int startIdx = packet.Length > 3 ? packet[3] : -1;

            if (startIdx == 2) System.Threading.Interlocked.Increment(ref _s2Count);
            else if (startIdx == 4) System.Threading.Interlocked.Increment(ref _s4Count);
            else if (startIdx == 6) System.Threading.Interlocked.Increment(ref _s6Count);
            else System.Threading.Interlocked.Increment(ref _sOtherCount);

            long s2 = System.Threading.Interlocked.Read(ref _s2Count);
            long s4 = System.Threading.Interlocked.Read(ref _s4Count);
            long s6 = System.Threading.Interlocked.Read(ref _s6Count);
            long other = System.Threading.Interlocked.Read(ref _sOtherCount);

            string logMsg = $"[{DateTime.Now:HH:mm:ss.fff}] [PKT][{layer}] #{total} Type={type} StartIdx={startIdx} Len={packet.Length} | Counts: s2={s2}, s4={s4}, s6={s6}, other={other}";
            System.Diagnostics.Trace.WriteLine(logMsg);

            lock (_pktLogLock)
            {
                try
                {
                    string logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "perf.log");
                    System.IO.File.AppendAllText(logPath, $"{logMsg}{Environment.NewLine}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PERF] LogPacket exception: {ex.Message}");
                }
            }
        }

        public static string GetPacketCountsSummary()
        {
            long total = System.Threading.Interlocked.Read(ref _pktCount);
            long s2 = System.Threading.Interlocked.Read(ref _s2Count);
            long s4 = System.Threading.Interlocked.Read(ref _s4Count);
            long s6 = System.Threading.Interlocked.Read(ref _s6Count);
            long other = System.Threading.Interlocked.Read(ref _sOtherCount);
            return $"Pkts: total={total} [s2={s2}, s4={s4}, s6={s6}, other={other}]";
        }


        public void InitialPort()
        {
            port = new SerialPort
            {
                BaudRate = BaudRate,
                DataBits = 8,
                Handshake = Handshake.None,
                Parity = Parity.None,
                PortName = PortName,
                StopBits = StopBits.One,
                ReadTimeout = 500,
                WriteTimeout = 2000
            };
        }
        public bool WriteData(string data)
        {
            try
            {
                if (!port.IsOpen)
                {
                    port.Open();
                }
                this.port.ReadExisting();
                this.port.Write(data);
                int toread = 1;
                int offset = 0;
                char[] result = new char[toread];
                while (toread > 0)
                {
                    int r = this.port.Read(result, offset, toread);
                    offset += r;
                    toread -= r;
                }

                if (port.IsOpen)
                {
                    port.Close();
                }

                if (result[0] == '0')
                {
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                if (port.IsOpen)
                {
                    port.Close();
                }
                return false;
            }
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
        public bool WriteDataInByte(byte[] data)
        {
            try
            {
                if (!port.IsOpen)
                {
                    port.Open();
                }

                ushort crc = ComputeCRC(data, data.Length-2);

                byte crcLow = (byte)(crc & 0xFF);
                byte crcHigh = (byte)(crc >> 8);

                data[data.Length - 2] = crcLow;
                data[data.Length - 1] = crcHigh;

                this.port.Write(data, 0, data.Length);

                int toread = 1;
                int offset = 0;
                char[] result = new char[toread];
                while (toread > 0)
                {
                    int r = this.port.Read(result, offset, toread);
                    offset += r;
                    toread -= r;
                }

                if (port.IsOpen)
                {
                    port.Close();
                }

                if (result[0] == '0')
                {
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                if (port.IsOpen)
                {
                    port.Close();
                }
                return false;
            }
        }


        public bool GetSystemStatus(string data)
        {
            try
            {

                if (!port.IsOpen)
                {
                    port.Open();
                }
                this.port.ReadExisting();
                this.port.Write(data);
                int toread = 1;
                int offset = 0;
                char[] result = new char[toread];
                while (toread > 0)
                {
                    int r = this.port.Read(result, offset, toread);
                    offset += r;
                    toread -= r;
                }

                if (result[0] == 21)
                {
                    IsTestOn = true;
                    busyStamp = System.DateTime.Now;
                }
                else if(result[0] == 22)
                {
                    IsTestOn = false;
                }
                return true;
            }
            catch (Exception)
            {
               
                return false;
            }
        }
    }

    public class Part
    {
        public string Name = "";
        public string CheckedBy = "";
        public string CompanyName = "";
        public int BatchSize = 5;
        public string Placce = "";
        public string Grade = "";
        public string ImagePath = "";
    }
    public class Configuration
    {
        public Marker Marker { get; set; } = new Marker();
        public Frequency Frequency { get; set; } = new Frequency();
        public Filter Filter { get; set; } = new Filter();

        public int TestTime = 10;
        public int SamplePerSecond = 3050;
        //public bool SaveGraphImage { get; set; }

    }

    public class ConfigurationToWrite
    {
        public int FC = 57;
        //public Marker Marker { get; set; }
        public List<FD> FQ { get; set; } = new List<FD>();
        public List<FilterFD> FT { get; set; } = new List<FilterFD>();        
    }
    public class Marker
    {
        public int FC = 50;
        public int M1 = 600;
        public int M2 = 1000;
        public int FmS = 500;
        public int RmS = 500;
        public int P1mS = 200;

        public int C1C2 = 245;
        public int C2E = 1060;
        public int CC2 = 110;
        public int MABC = 600;
    }

    public class FD
    {
        public int FN = 0;
        public int E = 1;
        public int F = 10000;
        public decimal G = 30;
        public int UTH = 90;
        public int LTH = 40;
        public int TH = 0;
        public int PP = 100;
        public int AT = -1;
    }
    public class FilterFD
    {
        public int FN = 0;
        public int H = 10;
        public decimal G = 30;
        public int L = 100;
        public int X = 10;
        public int Y = 10;
    }

    public class FilterFD1
    {
        public int FN = 0;
        public int H = 10;
        public int L = 100;
    }

    public class Frequency
    {
        public int FC = 51;
        public List<FD> FD = new List<FD>();
    }
    public class Filter
    {
        public int FC = 52;
        public List<FilterFD> FD = new List<FilterFD>();
    }

    public class Filter1
    {
        public int FC = 52;
        public List<FilterFD1> FD = new List<FilterFD1>();
    }
    public class GraphData
    {
        public bool Result = true;
        public List<Fdata> AmpD1 = new List<Fdata>();
        //public List<Fdata> AmpD2 = new List<Fdata>();
        //public List<Fdata> AmpD3 = new List<Fdata>();
        //public List<int> D1MarkerIndexs = new List<int>();
        //public List<int> D2MarkerIndexs = new List<int>();
        //public List<int> D3MarkerIndexs = new List<int>();
    }

    public class FNData
    {
        public int FN { get; set; }
        public List<Fdata> Data { get; set; } = new List<Fdata>();
    }

    public class Fdata
    {
        public int Amp { get; set; }
        public int Amp_ABS { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public int phase { get; set; }
        public bool IsMarked { get; set; }
    }

    public class Status
    {
        public int FC;
    }
    public class LogData
    {
        public string BatchName { get; set; } = string.Empty;
        public string LogStartDate { get; set; } = string.Empty;
        public string LogEndDate { get; set; } = string.Empty;
        public int PassCount { get; set; }
        public int FailCount { get; set; }
        public int TotalCount { get; set; }

    }
}

