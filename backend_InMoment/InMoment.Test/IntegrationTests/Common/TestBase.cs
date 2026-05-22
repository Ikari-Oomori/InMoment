using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace InMoment.IntegrationTests.Common;

public abstract class TestBase
{
    protected static StringContent Json(object obj)
    {
        return new StringContent(
            JsonSerializer.Serialize(obj),
            Encoding.UTF8,
            "application/json");
    }

    protected static void SetAuth(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}