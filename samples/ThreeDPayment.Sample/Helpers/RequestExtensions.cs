using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace ThreeDPayment.Sample.Helpers
{
    public static class RequestExtensions
    {
        public static string GetHostUrl(this HttpRequest request, bool appendSlash = true)
        {
            string hostUrl = request.Headers[HeaderNames.Host];

            if (StringValues.IsNullOrEmpty(hostUrl))
                hostUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";

            if (!hostUrl.StartsWith("http://") || !hostUrl.StartsWith("https://"))
                hostUrl = $"{request.Scheme}://{hostUrl}";

            if (appendSlash)
                hostUrl = $"{hostUrl.Trim('/')}/";

            return hostUrl;
        }
    }
}