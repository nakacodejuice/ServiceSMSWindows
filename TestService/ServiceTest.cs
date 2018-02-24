using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceSMS;

namespace TestService
{   
    class ServiceTest : MainService
    {
   
        public void TestStart(string[] args)
        {
            OnStart(args);
        }

        public void TestStop()
        {
            OnStop();
        }

        public void TestPause()
        {
            OnPause();
        }

        public void TestContinue()
        {
            OnContinue();
        }
    }
}
