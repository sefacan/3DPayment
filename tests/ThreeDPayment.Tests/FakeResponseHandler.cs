using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreeDPayment.Tests
{
    public class FakeResponseHandler : DelegatingHandler
    {
        private readonly IList<HttpResponseMessage> _fakeResponses = new List<HttpResponseMessage>();

        public void AddFakeResponse(HttpResponseMessage responseMessage, string content = "", bool xml = false)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                if (xml)
                {
                    responseMessage.Content = new StringContent(content, Encoding.UTF8, "application/xml");
                }
                else
                {
                    responseMessage.Content = new StringContent(content, Encoding.UTF8, "application/json");
                }
            }

            _fakeResponses.Add(responseMessage);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_fakeResponses.FirstOrDefault());
        }
    }
}