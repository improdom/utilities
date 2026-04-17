using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace PowerBiSpnInventory
{
    public sealed class PowerBiArtifact
    {
        public string ArtifactKind { get; set; } = "";
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? WebUrl { get; set; }
        public string RawJson { get; set; } = "";
    }

    public sealed class PowerBiOptions
    {
        public string TenantId { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public string WorkspaceId { get; set; } = "";
    }

    public static class PowerBiSpnClient
    {
        private static readonly string[] Scopes =
        {
            "https://analysis.windows.net/powerbi/api/.default"
        };

        public static async Task<string> GetAccessTokenAsync(
            PowerBiOptions options,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(options.TenantId))
                throw new ArgumentException("TenantId is required.");
            if (string.IsNullOrWhiteSpace(options.ClientId))
                throw new ArgumentException("ClientId is required.");
            if (string.IsNullOrWhiteSpace(options.ClientSecret))
                throw new ArgumentException("ClientSecret is required.");

            string authority = $"https://login.microsoftonline.com/{options.TenantId}";

            var app = ConfidentialClientApplicationBuilder
                .Create(options.ClientId)
                .WithAuthority(authority)
                .WithClientSecret(options.ClientSecret)
                .Build();

            var result = await app
                .AcquireTokenForClient(Scopes)
                .ExecuteAsync(cancellationToken);

            return result.AccessToken;
        }

        public static HttpClient CreateHttpClient(string accessToken)
        {
            var handler = new HttpClientHandler
            {
                UseProxy = true,
                Proxy = WebRequest.DefaultWebProxy,
                DefaultProxyCredentials = CredentialCache.DefaultCredentials,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(100)
            };

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        public static async Task<List<PowerBiArtifact>> GetWorkspaceArtifactsAsync(
            HttpClient client,
            string workspaceId,
            CancellationToken cancellationToken = default)
        {
            var artifacts = new List<PowerBiArtifact>();

            await AddReportsAsync(client, workspaceId, artifacts, cancellationToken);
            await AddDatasetsAsync(client, workspaceId, artifacts, cancellationToken);
            await AddDashboardsAsync(client, workspaceId, artifacts, cancellationToken);
            await AddDataflowsAsync(client, workspaceId, artifacts, cancellationToken);

            return artifacts
                .OrderBy(x => x.ArtifactKind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static async Task AddReportsAsync(
            HttpClient client,
            string workspaceId,
            List<PowerBiArtifact> artifacts,
            CancellationToken ct)
        {
            string url = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/reports";
            using var response = await client.GetAsync(url, ct);
            string json = await response.Content.ReadAsStringAsync(ct);

            EnsureSuccess(response, json, "reports");

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("value", out var values))
                return;

            foreach (var item in values.EnumerateArray())
            {
                artifacts.Add(new PowerBiArtifact
                {
                    ArtifactKind = "Report",
                    Id = GetString(item, "id"),
                    Name = GetString(item, "name"),
                    WebUrl = TryGetString(item, "webUrl"),
                    RawJson = item.GetRawText()
                });
            }
        }

        private static async Task AddDatasetsAsync(
            HttpClient client,
            string workspaceId,
            List<PowerBiArtifact> artifacts,
            CancellationToken ct)
        {
            string url = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/datasets";
            using var response = await client.GetAsync(url, ct);
            string json = await response.Content.ReadAsStringAsync(ct);

            EnsureSuccess(response, json, "datasets");

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("value", out var values))
                return;

            foreach (var item in values.EnumerateArray())
            {
                artifacts.Add(new PowerBiArtifact
                {
                    ArtifactKind = "Dataset",
                    Id = GetString(item, "id"),
                    Name = GetString(item, "name"),
                    WebUrl = TryGetString(item, "webUrl"),
                    RawJson = item.GetRawText()
                });
            }
        }

        private static async Task AddDashboardsAsync(
            HttpClient client,
            string workspaceId,
            List<PowerBiArtifact> artifacts,
            CancellationToken ct)
        {
            string url = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/dashboards";
            using var response = await client.GetAsync(url, ct);
            string json = await response.Content.ReadAsStringAsync(ct);

            EnsureSuccess(response, json, "dashboards");

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("value", out var values))
                return;

            foreach (var item in values.EnumerateArray())
            {
                artifacts.Add(new PowerBiArtifact
                {
                    ArtifactKind = "Dashboard",
                    Id = GetString(item, "id"),
                    Name = TryGetString(item, "displayName")
                           ?? TryGetString(item, "name")
                           ?? "",
                    WebUrl = TryGetString(item, "webUrl"),
                    RawJson = item.GetRawText()
                });
            }
        }

        private static async Task AddDataflowsAsync(
            HttpClient client,
            string workspaceId,
            List<PowerBiArtifact> artifacts,
            CancellationToken ct)
        {
            string url = $"https://api.powerbi.com/v1.0/myorg/groups/{workspaceId}/dataflows";
            using var response = await client.GetAsync(url, ct);
            string json = await response.Content.ReadAsStringAsync(ct);

            EnsureSuccess(response, json, "dataflows");

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("value", out var values))
                return;

            foreach (var item in values.EnumerateArray())
            {
                artifacts.Add(new PowerBiArtifact
                {
                    ArtifactKind = "Dataflow",
                    Id = TryGetString(item, "objectId")
                         ?? TryGetString(item, "id")
                         ?? "",
                    Name = TryGetString(item, "name")
                           ?? TryGetString(item, "displayName")
                           ?? "",
                    RawJson = item.GetRawText()
                });
            }
        }

        private static void EnsureSuccess(HttpResponseMessage response, string body, string area)
        {
            if (response.IsSuccessStatusCode)
                return;

            throw new HttpRequestException(
                $"Power BI call for '{area}' failed. " +
                $"Status={(int)response.StatusCode} {response.ReasonPhrase}. " +
                $"Body={body}");
        }

        private static string GetString(JsonElement element, string propertyName)
            => TryGetString(element, propertyName) ?? "";

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop))
                return null;

            return prop.ValueKind switch
            {
                JsonValueKind.String => prop.GetString(),
                JsonValueKind.Number => prop.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => prop.ToString()
            };
        }
    }

    internal static class Program
    {
        private static async Task Main()
        {
            var options = new PowerBiOptions
            {
                TenantId = Environment.GetEnvironmentVariable("PBI_TENANT_ID") ?? "",
                ClientId = Environment.GetEnvironmentVariable("PBI_CLIENT_ID") ?? "",
                ClientSecret = Environment.GetEnvironmentVariable("PBI_CLIENT_SECRET") ?? "",
                WorkspaceId = Environment.GetEnvironmentVariable("PBI_WORKSPACE_ID") ?? ""
            };

            try
            {
                string token = await PowerBiSpnClient.GetAccessTokenAsync(options);
                Console.WriteLine("Access token acquired.");

                using var client = PowerBiSpnClient.CreateHttpClient(token);

                var artifacts = await PowerBiSpnClient.GetWorkspaceArtifactsAsync(
                    client,
                    options.WorkspaceId);

                Console.WriteLine($"Artifacts found: {artifacts.Count}");
                foreach (var item in artifacts)
                {
                    Console.WriteLine($"{item.ArtifactKind,-10} {item.Name} ({item.Id})");
                }
            }
            catch (MsalServiceException ex)
            {
                Console.WriteLine("MSAL service error:");
                Console.WriteLine(ex.Message);
            }
            catch (MsalClientException ex)
            {
                Console.WriteLine("MSAL client error:");
                Console.WriteLine(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine("HTTP error:");
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected error:");
                Console.WriteLine(ex);
            }
        }
    }
}
