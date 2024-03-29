using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SR201_Application
{
    class Program
    {
        const int DEFAULTCYCLETIME = 10, NORMALCYCLETIME = 60;
        const string HOST = "192.168.1.100";
        const int PORT = 6722, CHANNEL = 8;
        const int TIME_UPDATE_INTERVAL = 60 * 60 * 1000, CYCLETIME = DEFAULTCYCLETIME * 1000;//In second

        static int start_hour = 0, start_min = 0, end_hour = 0, end_min = 0;
        static RelayController c;
        static TimeController t = new TADAdapter(TIME_UPDATE_INTERVAL);
        static DateTime lastUpdate = new DateTime();
        static Boolean isRunning = false, isDefault = true;
        static Timer t1, t2;

        static void Main(string[] args)//(ip, port, channel, cycletime of updating)<----all are optional
        {
            Console.WriteLine("--------------------------------------------------------------");
            
            string host = HOST;
            int port = PORT, channel = CHANNEL, cycleTime = CYCLETIME;

            int tMonth = DateTime.Now.Month;

            try
            {
                if (args[0] != null)
                    host = args[0];
            }
            catch (IndexOutOfRangeException e) { }
            try
            {
                if (args[1] != null)
                    port = int.Parse(args[1]);
            }
            catch (IndexOutOfRangeException e) { }
            try
            {
                if (args[2] != null)
                    channel = int.Parse(args[2]);
            }
            catch (IndexOutOfRangeException e) { }
            try
            {
                if (args[3] != null)
                    cycleTime = int.Parse(args[3]);
            }
            catch (IndexOutOfRangeException e) { }

            try
            {
                c = new RelayController(host, port, channel);

                t1 = new Timer(new TimerCallback((Object obj) =>
                {
                    DateTime now = DateTime.Now;
                    if (now.Date != lastUpdate.Date || isDefault)
                    {
                        updateTime();
                    }
                }), new AutoResetEvent(true), 1000, TIME_UPDATE_INTERVAL);

                
                t2  = new Timer(new TimerCallback((Object obj) =>
                {
                    updateRelay();
                    
                }), new AutoResetEvent(true), 1000, cycleTime);

                string line = null;

                while (line != "EXIT")
                {
                    Console.WriteLine("\nType \"Exit\" if you leave." +
                        "\n\"LT\", for showing last update date." +
                        "\n\"R\", for update relay status immediately." +
                        "\n\"UR\", for update the cycle time for relay update period immediately." +
                        "\n\"T\", for update time immediately.\n"
                        );
                    line = Console.ReadLine().ToUpper();

                    switch (line)
                    {
                        case "LT":
                            Console.WriteLine("Last time to request sunrise and sunset time is " + lastUpdate.ToLongDateString());
                            Console.WriteLine("The light stops between " + start_hour.ToString().PadLeft(2, '0') + ":" + start_min.ToString().PadLeft(2, '0') + " and " + end_hour.ToString().PadLeft(2, '0') + ":" + end_min.ToString().PadLeft(2, '0') + ".");
                            break;
                        case "R":
                            updateRelay();
                            break;
                        case "T":
                            updateTime();
                            break;
                        case "UR":
                            while (true)
                            {
                                Console.WriteLine("Really updates in every " + cycleTime / 1000 + " second. Please enter new update time, or type \"E\" to cancel.");
                                string input = Console.ReadLine();
                                int time = 0;
                                bool isValid = Int32.TryParse(input, out time);
                                if (isValid && time>0)
                                {                                   
                                    cycleTime = time * 1000;
                                    t2.Change(1000, cycleTime);
                                    Console.WriteLine("Updated.");
                                    break;
                                }
                                else
                                {
                                    if (input.ToUpper() == "E")
                                        break;
                                    Console.WriteLine("Invalid input!");
                                }
                                 
                            }
                            break;
                        case "Z":
                            throw new Exception();
                        case "EXIT":
                        default:
                            break;
                    }
                }
            } catch(Exception e)
            {
                t1.Dispose();
                t2.Dispose();
                Main(new string[]{ host, port.ToString(), channel.ToString(), cycleTime.ToString() });
                Debugger.print(e.Message);
            }
        }

        static void updateRelay()
        {
            if (isRunning)
            {
                DateTime now = DateTime.Now;
                int hour = now.Hour, min = now.Minute;
                char status = RelayController.ON;

                if ((hour > start_hour && hour < end_hour) || ((hour == start_hour && min >= start_min) || (hour == end_hour && min <= end_min)))
                    status = RelayController.OFF;


                if (c.updateAllStatus(status))
                    Debugger.print("UPDATED Relay Status.");
            }
        }

        static void updateTime()
        {
           
                int[] result = t.GetResult();

                start_hour = result[0];
                start_min = result[1];
                end_hour = result[2];
                end_min = result[3];

                Debugger.print("Updated. The light stops between " + start_hour.ToString().PadLeft(2, '0') + ":" + start_min.ToString().PadLeft(2, '0') + " and " + end_hour.ToString().PadLeft(2, '0') + ":" + end_min.ToString().PadLeft(2, '0') + ".");

                lastUpdate = DateTime.Now;
                isRunning = true;
                isDefault = (result[4] == 0) ? true : false;
            
        }

        public abstract class TimeController
        {
            protected const int TIME_UPDATE_INTERVAL = 60 * 60 * 1000;

            protected const int SUMMER_START_HOUR = 6, SUMMER_END_HOUR = 17, WINTER_START_HOUR = 7, WINTER_END_HOUR = 16;

            private int interval = 0;
            private string url;

            protected TimeController(string url) : this(TIME_UPDATE_INTERVAL, url) { }

            public TimeController(int interval, string url)
            {
                this.interval = interval;
                this.url = url;
            }

            public abstract int[] GetResult();


            protected int GetInterval()
            {
                return this.interval;
            }

            protected string GetUrl()
            {
                return this.url;
            }
        }

        public class TADAdapter : TimeController
        {
            //string urlAddress = "http://www.weather.gov.hk/contente.htm";
            const string URL = "https://www.timeanddate.com/sun/hong-kong/hong-kong";

            public TADAdapter(int interval) : base(interval, URL) { }

            public override int[] GetResult()
            {
                int[] result = null;
                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(GetUrl());
                    request.ContentType = "text/html";
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Stream receiveStream = response.GetResponseStream();
                        StreamReader readStream = null;

                        readStream = new StreamReader(receiveStream);

                        string data = readStream.ReadToEnd();
                        Regex regex = new Regex(@"(?<=three>)([0-9]|0[0-9]|1[0-9]|2[0-3]):([0-5][0-9])", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        MatchCollection sss = regex.Matches(data);


                        string[] t1 = sss[0].ToString().Split(':');
                        string[] t2 = sss[1].ToString().Split(':');

                        result = new int[] { int.Parse(t1[0]), int.Parse(t1[1]), int.Parse(t2[0]), int.Parse(t2[1]), 1 };

                        response.Close();
                        readStream.Close();

                    }
                }
                catch (Exception ex)
                {

                    int tMonth = DateTime.Now.Month;

                    if (tMonth >= 11 || (tMonth >= 1 && tMonth <= 3))
                    {
                        result = new int[] { WINTER_START_HOUR, 0, WINTER_END_HOUR, 0, 0 };
                    }
                    else
                    {
                        result = new int[] { SUMMER_START_HOUR, 0, SUMMER_END_HOUR, 0, 0 };
                    }
                }
                finally
                {
                    GC.Collect();
                    GC.SuppressFinalize(this);
                }

                return result;//return {[start_hour], [start_minute], [end_hour], [end_minute], [isDefault, if return 0, then it returns const, otherwise they are from internet.]}
            }
        }

        class Debugger
        {
            public static void print(string msg)
            {
                Console.WriteLine(DateTime.Now + ": " + msg);
            }
        }

        class RelayController
        {
            public const char ON = '2', OFF = '1';
            private string host;
            private int port, channel;
            private TcpClient client;

            public RelayController(string host, int port, int channel)
            {
                this.host = host;
                this.port = port;
                this.channel = channel;
                connect2Host();
            }

            public void connect2Host()
            {
                try
                {
                    Debugger.print("CONNECTING...");
                    if (this.client != null)
                        this.client.Dispose();
                    this.client = new TcpClient();
                    this.client.Connect(this.host, this.port);
                    Debugger.print("CONNECTED to " + host);
                }
                catch (SocketException e)
                {
                    this.client.Close();
                    Debugger.print("Host not FOUND.");
                }
            }

            public Boolean updateAllStatus(char status)
            {
                Boolean isFinished = false;
                string statuses = getStates();
                if(statuses==null)
                {
                    return false;
                }

                for (int i = 1; i <= channel; i++)
                {
                    if (status != statuses.ToCharArray()[i - 1])
                    {
                        if (i <= this.channel && i != 0)
                        {
                            string newStatuses = tcpSendMsg(status + i.ToString());
                            if (!(newStatuses.Equals(statuses)))
                            {
                                statuses = newStatuses;
                                isFinished = true;
                            }
                        }
                    }
                }


                return isFinished;
            }

            public Boolean updateStatus(int ch, char status)
            {
                Boolean isFinished = false;

                if (ch <= this.channel && ch != 0)
                {
                    string msg = status + ch.ToString();
                    tcpSendMsg(msg);
                }

                return isFinished;
            }
            private string getStates() { return tcpSendMsg("00"); }

            private string tcpSendMsg(string msg)
            {
                string result = null;

                                 
                    NetworkStream serverStream = null;

                    try
                    {
                        byte[] outStream = Encoding.ASCII.GetBytes(msg);
                        serverStream = this.client.GetStream();
                        serverStream.Write(outStream, 0, outStream.Length);
                        serverStream.Flush();

                        byte[] buffer = new byte[this.client.ReceiveBufferSize];
                        int bytesRead = serverStream.Read(buffer, 0, client.ReceiveBufferSize);
                        result = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    }
                    catch (Exception e)
                    {
                        connect2Host();
                        Debugger.print(e.Message);
                    }

                if (result != null)
                    result.Replace('0', '2');

                return result;
            }
        }
    }
}
