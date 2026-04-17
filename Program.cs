using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var url = "https://wabi-west-europe-b-primary-redirect.analysis.windows.net/metadata/relations/folder/6bc6f903-7ed1-4500-8663-b6154753c766";

        using (var client = new HttpClient())
        {
            var token = "YOUR_ACCESS_TOKEN";

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br, zstd");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

            client.DefaultRequestHeaders.Add("Origin", "https://app.powerbi.com");
            client.DefaultRequestHeaders.Add("Referer", "https://app.powerbi.com/");
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36 Edg/146.0.0.0");

            client.DefaultRequestHeaders.Add("X-PowerBI-HostEnv", "Power BI Web App");
            client.DefaultRequestHeaders.Add("X-Consuming-Feature", "ListView");

            var response = await client.GetAsync(url);

            var content = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine(content);
        }
    }
}
