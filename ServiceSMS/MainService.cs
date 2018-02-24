using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using SMSPDULib;
using System.Data.SQLite;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.ServiceModel;
using System.Web;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using Microsoft.Owin.Hosting;
using System.Web.Http.SelfHost;
using System.Web.Http;
using System.Xml;
using System.Xml.Linq;
using System.Data.Common;
using System.Windows.Forms;
namespace ServiceSMS
{
    public class SMSReader
    {
        public static bool stopSerialRead;
        public static bool test;
        public static string databaseName;
        public static string COMPort;
        public static SerialPort port;
        private stopSerialReadCallback callback;
        public static DateTime lastAT;

        public delegate void stopSerialReadCallback(bool stopSerialRead);
        public SMSReader(string txtdatabaseName,string PortCOM )
        {
            databaseName = txtdatabaseName;
            COMPort = PortCOM;
        }
        public void ReadProc()
        {
            if (port != null)
            {
                if (!port.IsOpen)
                {
                    port = new SerialPort();
                    OpenPort(COMPort);
                }
            }
            else
            {
                port = new SerialPort();
                OpenPort(COMPort);
            }
            stopSerialRead = false;
            while (!stopSerialRead)
            {
                if (!port.IsOpen)
                {
                    port = new SerialPort();
                    OpenPort(COMPort);
                }
                //Console.WriteLine(test);
                readSMS();
                //Thread.Sleep(10000);
                QueueSendSMS();
                //if (port.IsOpen) port.Close();
                Thread.Sleep(15000);
                TimeSpan span = DateTime.Now - lastAT;
                DateTime relative = new DateTime(span.Ticks);
                if(relative.Minute>600)

                    MessageBox.Show("Модем помер!!!");
               
                //readBalance();
            }
        }

        private static void QueueSendSMS()
        {

            SQLiteConnection db4 = new SQLiteConnection(string.Format("Data Source={0}; Version=3;", MainService.st.SQlitePath));
            SQLiteCommand Command4 = db4.CreateCommand();
            Command4.CommandText = "select * from  queue limit 10";
            db4.Open();
            SQLiteDataReader reader4 = Command4.ExecuteReader();
            //List<StructRes> list = new List<StructRes>();
            foreach (DbDataRecord record in reader4)
            {
                bool res = sendSMS(Convert.ToString(record["message"]), Convert.ToString(record["tel"]));

                if(res)
                {
                    SQLiteCommand Command6 = db4.CreateCommand();
                    Command6.CommandText = "delete from  queue where UID ='"+Convert.ToString(record["UID"])+"'";
                    Command6.ExecuteNonQuery();
                    SQLiteCommand Command5 = db4.CreateCommand();
                    Command5.CommandText = "insert into smssendhistory (date,tel,message,UID) values (@Date,@tel,@message,@UID)";
                    string Fmt = "yyyy-MM-dd HH:mm:ss";//2016-07-01 00:00:00
                    DateTime d = DateTime.Now;
                    string strUID = Convert.ToString(Guid.NewGuid());
                    Command5.Parameters.AddWithValue("@Date", d.ToString(Fmt));
                    Command5.Parameters.AddWithValue("@tel", Convert.ToString(record["tel"]));
                    Command5.Parameters.AddWithValue("@message", Convert.ToString(record["message"]));
                    Command5.Parameters.AddWithValue("@UID", Convert.ToString(record["UID"]));
                    Command5.ExecuteNonQuery();
                }
            }
            db4.Close();


        }


        private static bool sendSMS(string textsms, string telnumber)
        {
            if (!port.IsOpen) return false;

            try
            {
                System.Threading.Thread.Sleep(500);
                port.WriteLine("AT\r\n"); // означает "Внимание!" для модема 
                System.Threading.Thread.Sleep(500);

                port.Write("AT+CMGF=0\r\n"); // устанавливается цифровой режим PDU для отправки сообщений
                System.Threading.Thread.Sleep(500);
            }
            catch
            {
                return false;
            }

            try
            {
                telnumber = telnumber.Replace("-", "").Replace(" ", "").Replace("+", "");

                // 01 это PDU Type или иногда называется SMS-SUBMIT. 01 означает, что сообщение передаваемое, а не получаемое 
                // цифры 00 это TP-Message-Reference означают, что телефон/модем может установить количество успешных сообщений автоматически
                // telnumber.Length.ToString("X2") выдаст нам длинну номера в 16-ричном формате
                // 91 означает, что используется международный формат номера телефона
                telnumber = "01" + "00" + telnumber.Length.ToString("X2") + "91" + EncodePhoneNumber(telnumber);

                textsms = StringToUCS2(textsms);
                // 00 означает, что формат сообщения неявный. Это идентификатор протокола. Другие варианты телекс, телефакс, голосовое сообщение и т.п.
                // 08 означает формат UCS2 - 2 байта на символ. Он проще, так что рассмотрим его.
                // если вместо 08 указать 18, то сообщение не будет сохранено на телефоне. Получится flash сообщение
                string leninByte = (textsms.Length / 2).ToString("X2");
                textsms = telnumber + "00" + "08" + leninByte + textsms;

                // посылаем команду с длинной сообщения - количество октет в десятичной системе. то есть делим на два количество символов в сообщении
                // если октет неполный, то получится в результате дробное число. это дробное число округляем до большего
                double lenMes = textsms.Length / 2;
                port.Write("AT+CMGS=" + (Math.Ceiling(lenMes)).ToString() + "\r\n");
                System.Threading.Thread.Sleep(500);

                // номер sms-центра мы не указываем, считая, что практически во всех SIM картах он уже прописан
                // для того, чтобы было понятно, что этот номер мы не указали добавляем к нашему сообщению в начало 2 нуля
                // добавляем именно ПОСЛЕ того, как подсчитали длинну сообщения
                textsms = "00" + textsms;

                port.Write(textsms + char.ConvertFromUtf32(26) + "\r\n");
                System.Threading.Thread.Sleep(500);
            }
            catch
            {
                return false;
            }

            try
            {
                string recievedData;
                recievedData = port.ReadExisting();

                if (recievedData.Contains("ERROR"))
                {
                    return false;
                }

            }
            catch { }

            return true;
        }

        private static string readSMS()
        {
            if (!port.IsOpen) return "Error";
            try
            {
                System.Threading.Thread.Sleep(4000);
                port.WriteLine("AT\r\n"); // означает "Внимание!" для модема 
                System.Threading.Thread.Sleep(4000);

                port.Write("AT+CMGL=4\r\n"); // устанавливается цифровой режим PDU для отправки сообщений
                System.Threading.Thread.Sleep(4000);
            }
            catch
            {
                return "Error";
            }

            return "";
        }

        private static string readBalance()
        {
            if (!port.IsOpen) return "Error";
            try
            {
                System.Threading.Thread.Sleep(3000);
                port.WriteLine("AT\r\n"); // означает "Внимание!" для модема 
                System.Threading.Thread.Sleep(3000);

                port.Write("AT+CUSD=1,AA180C3602,15\r\n"); // устанавливается цифровой режим PDU для отправки сообщений
                //System.Threading.Thread.Sleep(500);
            }
            catch
            {
                return "Error";
            }

            return "";
        }

        static void OpenPort(string PortCOM)
        {

            port.BaudRate = 9600; //2400; // еще варианты 4800, 9600, 28800 или 56000
            port.DataBits = 7; // еще варианты 8, 9

            port.StopBits = StopBits.One; // еще варианты StopBits.Two StopBits.None или StopBits.OnePointFive         
            port.Parity = Parity.Odd; // еще варианты Parity.Even Parity.Mark Parity.None или Parity.Space

            port.ReadTimeout = 500; // еще варианты 1000, 2500 или 5000 (больше уже не стоит)
            port.WriteTimeout = 500; // еще варианты 1000, 2500 или 5000 (больше уже не стоит)

            //port.Handshake = Handshake.RequestToSend;
            //port.DtrEnable = true;
            //port.RtsEnable = true;
            //port.NewLine = Environment.NewLine;

            port.Encoding = Encoding.GetEncoding("windows-1251");

            port.PortName = PortCOM;

            // незамысловатая конструкция для открытия порта
            if (port.IsOpen)
                port.Close();
            try
            {
                port.Open();
                port.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private static void DataReceivedHandler(
                        object sender,
                        SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            int f;
            //Console.Write(indata);
            if (indata.IndexOf("OK")>0) 
                lastAT = DateTime.Now;
            if (indata.IndexOf("CMGL:") > 0)
            {
                string[] delimiter = new string[] { "CMGL:" };
                String[] substrings = indata.Split(delimiter, StringSplitOptions.None);
                foreach (string substr in substrings)
                {
                    if ((substr.IndexOf(",,") != -1))
                    {
                        //Console.WriteLine(substr);
                        try
                        {
                            f = Convert.ToInt32(substr.IndexOf(","));
                        }
                        catch
                        {
                            continue;  

                        }
                        string nummes = substr.Substring(1, f-1);
                        int n = substr.IndexOf('\n');
                        string str ="";
                        if(n!=-1)
                            str = substr.Substring(n+1, substr.Length - n-2);
                        else str = substr;
                        n = str.IndexOf('\r');
                        if(n!=-1)
                            str = str.Substring(0, n);
                        SMSType smsType = SMSBase.GetSMSType(str);
                        switch (smsType)
                        {
                            case SMSType.SMS:
                                SMS sms = new SMS();
                                SMS.Fetch(sms, ref str);
                                Console.WriteLine(sms.Message);
                                Console.WriteLine(sms.PhoneNumber);
                                //string databaseName = @"C:\Users\Rootkit\Desktop\WindowsService-master\ServiceSMS\bin\Debug\readsms.db";
                                SQLiteConnection db = new SQLiteConnection(string.Format("Data Source={0}; Version=3;", databaseName));
                                SQLiteCommand Command = db.CreateCommand();
                                Command.CommandText = "insert into smshistory (date,tel,message,UID) values (@Date,@tel,@message,@UID)";
                                string customFmt = "yyyy-MM-dd HH:mm:ss";//2016-07-01 00:00:00
                                Command.Parameters.AddWithValue("@Date", sms.ServiceCenterTimeStamp.ToString(customFmt));
                                Command.Parameters.AddWithValue("@tel", sms.PhoneNumber);
                                Command.Parameters.AddWithValue("@message", sms.Message);
                                Command.Parameters.AddWithValue("@UID", Convert.ToString(Guid.NewGuid()));
                                if (sms.Message !=null)
                                { 
                                    db.Open();
                                    Command.ExecuteNonQuery();
                                    db.Close();
                                }
                                port.Write("AT+CMGD=" + nummes + "\r\n");
                                //Console.WriteLine(sms._phoneNumber);
                                break;
                        }
                        //string nummessage = substring.Substring(1, 3);
                        //Console.WriteLine(nummessage);
                    }
                }
            }


        }

        // перекодирование номера телефона для формата PDU
        public static string EncodePhoneNumber(string PhoneNumber)
        {
            string result = "";
            if ((PhoneNumber.Length % 2) > 0) PhoneNumber += "F";

            int i = 0;
            while (i < PhoneNumber.Length)
            {
                result += PhoneNumber[i + 1].ToString() + PhoneNumber[i].ToString();
                i += 2;
            }
            return result.Trim();
        }



        // перекодирование текста смс в UCS2 
        public static string StringToUCS2(string str)
        {
            UnicodeEncoding ue = new UnicodeEncoding();
            byte[] ucs2 = ue.GetBytes(str);

            int i = 0;
            while (i < ucs2.Length)
            {
                byte b = ucs2[i + 1];
                ucs2[i + 1] = ucs2[i];
                ucs2[i] = b;
                i += 2;
            }
            return BitConverter.ToString(ucs2).Replace("-", "");
        }
    }

    public partial class MainService : ServiceBase
    {

        public static bool stopSerialRead;
        //public static StConfig st = readconfig("config.xml", "configRestSMS", "SqlitePath");
        //static SMSReader smsreader = new SMSReader(st.SQlitePath,st.DongleCOM);*/
        public static StConfig st;
        static SMSReader smsreader;
        static ProductService server;
        static WebHttpBehavior behavior;
        Thread smsreaderproc;

        public struct StConfig
        {
            public string SQlitePath, DongleCOM,IP;

            public StConfig(string p1, string p2, string IP2)
            {
                SQlitePath = p1;
                DongleCOM = p2;
                IP = IP2;
            }
        }

        public MainService(string[] args)
        {
            st = readconfig(args[0], "configRestSMS", "SqlitePath");
            smsreader = new SMSReader(st.SQlitePath, st.DongleCOM);
            smsreaderproc = new Thread(new ThreadStart(smsreader.ReadProc));
            server = new ProductService();
            behavior = new WebHttpBehavior();
            InitializeComponent();


        }

        public MainService()
        {
            st = readconfig("config.xml", "configRestSMS", "SqlitePath");
            smsreader = new SMSReader(st.SQlitePath, st.DongleCOM);
            smsreaderproc = new Thread(new ThreadStart(smsreader.ReadProc));
            server = new ProductService();
            behavior = new WebHttpBehavior();
            InitializeComponent();


        }

        protected override void OnStart(string[] args)
        {
            smsreaderproc.Start();
            server.Start();
 

        }

        protected override void OnStop()
        {
            smsreaderproc.Abort();
            server.Stop();
        }

        protected override void OnPause()
        {
            //stopSerialRead = true;
        }

        protected override void OnContinue()
        {
            string[] args = { "", "" };
            OnStart(args);
        }

        public static StConfig readconfig(string pathtoXML, string RootElement, string Node)
        {
            StConfig cf = new StConfig();
            // XElement el = new XElement();
            using (XmlReader reader = XmlReader.Create(pathtoXML))
            {
                while (reader.Read())
                {
                    if (reader.Name=="SqlitePath")
                    {
                        reader.Read();
                        if(reader.Value.IndexOf('\n') ==-1)
                            cf.SQlitePath = reader.Value;
                    }
                      else if (reader.Name=="DongleCOM")
                        {
                            reader.Read();
                            if (reader.Value.IndexOf('\n') == -1)
                                cf.DongleCOM = reader.Value;
                        }
                    else if (reader.Name == "IP")
                    {
                        reader.Read();
                        if (reader.Value.IndexOf('\n') == -1)
                            cf.IP = reader.Value;
                    }
                }



            }
            return cf;
        }
    
    }



    class ProductService
    {
        private readonly HttpSelfHostServer server;

        public ProductService()
        {
            var selfHostConfiguraiton = new HttpSelfHostConfiguration("http://"+MainService.st.IP+":8181");
            //selfHostConfiguraiton.HostNameComparisonMode = HostNameComparisonMode.Exact;
            //selfHostConfiguraiton.EnableCors();
            //EnableCrossDmainAjaxCall();
            selfHostConfiguraiton.Routes.MapHttpRoute(
                name: "DefaultApiRoute",
                routeTemplate: "api/{controller}",
                defaults: new { id = RouteParameter.Optional }
                );

            server = new HttpSelfHostServer(selfHostConfiguraiton);
            /*using (var server = new HttpSelfHostServer(selfHostConfiguraiton))
            {
                server.OpenAsync().Wait();
                Console.ReadLine();
            }*/
        }

        private void EnableCrossDmainAjaxCall()
        {


            if (HttpContext.Current.Request.HttpMethod == "OPTIONS")
            {
                HttpContext.Current.Response.AddHeader("Access-Control-Allow-Methods",
                              "GET, POST");
                HttpContext.Current.Response.AddHeader("Access-Control-Allow-Headers",
                              "Content-Type, Accept");
                HttpContext.Current.Response.AddHeader("Access-Control-Max-Age",
                              "1728000");
                HttpContext.Current.Response.End();
            }
        }

        public void Start()
        {
            server.OpenAsync().Wait();
        }

        public void Stop()
        {
            server.CloseAsync();
            server.Dispose();
        }

    }

    public class ServiceRest
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

}
