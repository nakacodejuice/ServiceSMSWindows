using System.Collections.Generic;
using System.Web.Http;
using System.Net.Http;

namespace AspNetSelfHostDemo
{
    public class DemoController : ApiController
    {
        // GET api/demo 
        public string Get()
        {
            return "Please, POST only!";
        }


        // POST api/demo 
        public HttpResponseMessage Post(HttpRequestMessage request)
        {
            var someText = request.Content.ReadAsStringAsync().Result;
            return new HttpResponseMessage() { Content = new StringContent(someText) };
        }

    } 
}
