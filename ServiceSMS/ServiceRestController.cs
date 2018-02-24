using System.Collections.Generic;
using System.Web.Http;
using System.Net.Http;
using Newtonsoft.Json;
using System.Net;
using System.Data.SQLite;
using System;
using System.Data.Common;


namespace ServiceSMS
{
    public class ServiceRestController : ApiController
    {
        // GET api/ServiceRest 
        public string Get()
        {
            GetStruct st = new GetStruct();
            st.balance = "дофига";
            st.port = SMSReader.port.IsOpen;
            st.lasATCommand = SMSReader.lastAT;
            st.COM = MainService.st.DongleCOM;
            string json = JsonConvert.SerializeObject(st);
            return json;
        }


        // POST api/ServiceRest 
        public HttpResponseMessage Post(HttpRequestMessage request)
        {
            var json = request.Content.ReadAsStringAsync().Result;
            //try
            //{
            Data data = JsonConvert.DeserializeObject<Data>(json);
            ResponseData resdata = new ResponseData();
            List<StructRes> list = new List<StructRes>();
            switch (data.action)
            {
                
                case "GetNewSMS":
                    SQLiteConnection db = new SQLiteConnection(string.Format("Data Source={0}; Version=3;", MainService.st.SQlitePath));
                    SQLiteCommand Command = db.CreateCommand();
                    Command.CommandText = "select * from  smshistory where not isread";
                    db.Open();
                    SQLiteDataReader reader = Command.ExecuteReader();
                    //List<StructRes> list = new List<StructRes>();
                    foreach (DbDataRecord record in reader)
                    {
                        StructRes r = new StructRes();
                        r.date = Convert.ToDateTime(record["date"]);
                        r.tel = Convert.ToString(record["tel"]);
                        r.message = Convert.ToString(record["message"]);
                        r.UID = Convert.ToString(record["UID"]);
                        list.Add(r);
                    }
                    db.Close();
                break;
                case "GetSMSPeriod":
                    SQLiteConnection db4 = new SQLiteConnection(string.Format("Data Source={0}; Version=3;", MainService.st.SQlitePath));
                    SQLiteCommand Command4 = db4.CreateCommand();
                    Command4.CommandText = "select * from  smshistory where date between @Date1 and @Date2";
                    string F = "yyyy-MM-dd HH:mm:ss";//2016-07-01 00:00:00
                    Command4.Parameters.AddWithValue("@Date1", data.dataTime1.ToString(F));
                    Command4.Parameters.AddWithValue("@Date2", data.dataTime2.ToString(F));
                    db4.Open();
                    SQLiteDataReader reader4 = Command4.ExecuteReader();
                    //List<StructRes> list = new List<StructRes>();
                    foreach (DbDataRecord record in reader4)
                    {
                        StructRes r = new StructRes();
                        r.date = Convert.ToDateTime(record["date"]);
                        r.tel = Convert.ToString(record["tel"]);
                        r.message = Convert.ToString(record["message"]);
                        r.UID = Convert.ToString(record["UID"]);
                        r.isread = Convert.ToBoolean(record["isread"]);
                        list.Add(r);
                    }
                    db4.Close();
                break;
               case "SetIsRead":
                    SQLiteConnection db1 = new SQLiteConnection(string.Format("Data Source={0}; Version=3;", MainService.st.SQlitePath));
                    SQLiteCommand Command1 = db1.CreateCommand();
                    string listUID = "";
                    foreach (string UID in data.data1)
                    {
                        if (listUID =="")
                            listUID =  "'"+UID+"'";
                        else
                            listUID = listUID +",'"+ UID+"'"; 
                    }
                    Command1.CommandText = "update smshistory  set isread =1  where UID in (" + listUID + ")";
                    db1.Open();
                    Command1.ExecuteNonQuery();
                    db1.Close();
                break;

               case "AddSmsToQueue":
                    SQLiteConnection db2 = new SQLiteConnection(string.Format("Data Source={0}; Version=3;", MainService.st.SQlitePath));
                    SQLiteCommand Command2 = db2.CreateCommand();
                    Command2.CommandText = "insert into queue (date,tel,message,UID) values (@Date,@tel,@message,@UID)";
                    string Fmt = "yyyy-MM-dd HH:mm:ss";//2016-07-01 00:00:00
                    DateTime d = DateTime.Now;
                    string strUID = Convert.ToString(Guid.NewGuid());
                    Command2.Parameters.AddWithValue("@Date", d.ToString(Fmt));
                    Command2.Parameters.AddWithValue("@tel", data.data2);
                    Command2.Parameters.AddWithValue("@message", data.data3);
                    Command2.Parameters.AddWithValue("@UID", strUID);
                    db2.Open();
                    Command2.ExecuteNonQuery();
                    db2.Close();
                    StructRes r1 = new StructRes();
                    r1.date = d;
                    r1.tel = data.data2;
                    r1.message = data.data3;
                    r1.UID = strUID;
                    list.Add(r1);
                break;
            }
            //}
              //  catch {}
            resdata.list = list;
            resdata.date = DateTime.Now;
            resdata.action = data.action;
            json = JsonConvert.SerializeObject(resdata);
            return new HttpResponseMessage() { Content = new StringContent(json) };
        }

    }
    class Data
    {
        public string action;
        public List<string> data1;
        public string data2;
        public string data3;
        public DateTime dataTime1;
        public DateTime dataTime2;
    }

    class ResponseData
    {
        public string action;
        public DateTime date;
        public List<StructRes> list;
    }

    public struct StructRes
    {
        public string  tel,  message, UID;
        public DateTime date;
        public bool isread;

        public StructRes(DateTime date1, string tel1, string message1, string UID1,bool isread1)
        {
            date = date1;
            tel = tel1;
            message = message1;
            UID = UID1;
            isread = isread1;
        }
    }

    public struct GetStruct
    {
        public DateTime lasATCommand;
        public string balance;
        public string COM;
        public bool port;

        public GetStruct(DateTime lasATCommand1, string balance1, string COM1, bool port1)
        {
            lasATCommand = lasATCommand1;
            balance = balance1;
            COM = COM1;
            port = port1;
        }
    }
}
