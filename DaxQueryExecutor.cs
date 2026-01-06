Subject: Out of Office

Thank you for your message. I am out of the office on vacation starting today, December 15, 2025, and will return on January 5, 2026.

While I’m away:

For CubIQ-related questions or support, please contact Ramesh Revelli at ramesh@ubs.com
.

For escalations, please reach out to Siva at siva@ubs.com
.
i
I will respond to emails after I return.

Best regards,
Julio Diaz




using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public static class VaultSecretReader
{
    /// <summary>
    /// Replicates the PowerShell script logic:
    /// 1) POST /v1/auth/cert/login with client cert + namespace -> client_token
    /// 2) GET  /v1/secret/data/{secretPath} with token + namespace -> password (data.data.password)
    /// </summary>
    public static async Task<string> GetSecretPasswordAsync(
        string vaultUrl,
        string vaultNamespace,
        string vaultCertName,
        string vaultSecretPath,
        string? certificateThumbprint = null,
        string? certificateSubjectContains = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vaultUrl)) throw new ArgumentException("vaultUrl is required.", nameof(vaultUrl));
        if (string.IsNullOrWhiteSpace(vaultNamespace)) throw new ArgumentException("vaultNamespace is required.", nameof(vaultNamespace));
        if (string.IsNullOrWhiteSpace(vaultCertName)) throw new ArgumentException("vaultCertName is required.", nameof(vaultCertName));
        if (string.IsNullOrWhiteSpace(vaultSecretPath)) throw new ArgumentException("vaultSecretPath is required.", nameof(vaultSecretPath));

        // If you're on .NET Framework, keep TLS 1.2 explicit:
        // ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        var cert = FindCertificateFromLocalMachineMy(certificateThumbprint, certificateSubjectContains);

        var handler = new HttpClientHandler
        {
            SslProtocols = SslProtocols.Tls12
        };
        handler.ClientCertificates.Add(cert);

        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(NormalizeBaseUrl(vaultUrl), UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(60)
        };

        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.Remove("X-Vault-Namespace");
        http.DefaultRequestHeaders.Add("X-Vault-Namespace", vaultNamespace);

        // 1) Login
        var loginUrl = "/v1/auth/cert/login";
        var loginPayload = new { name = vaultCertName };
        var loginJson = JsonSerializer.Serialize(loginPayload);
        using var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");

        using var loginResp = await http.PostAsync(loginUrl, loginContent, cancellationToken).ConfigureAwait(false);
        var loginBody = await loginResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!loginResp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Vault login failed: {(int)loginResp.StatusCode} {loginResp.ReasonPhrase}\n{loginBody}");

        string token = ExtractVaultClientToken(loginBody);

        // 2) Read secret
        var secretUrl = "/v1/secret/data/" + vaultSecretPath.TrimStart('/'); // PS: "$vault_url/v1/secret/data/$vault_secret_path"
        using var secretReq = new HttpRequestMessage(HttpMethod.Get, secretUrl);
        secretReq.Headers.Add("X-Vault-Token", token);

        using var secretResp = await http.SendAsync(secretReq, cancellationToken).ConfigureAwait(false);
        var secretBody = await secretResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!secretResp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Vault secret read failed: {(int)secretResp.StatusCode} {secretResp.ReasonPhrase}\n{secretBody}");

        string password = ExtractPassword(secretBody);

        return $"SECRET: {password}";
    }

    private static string NormalizeBaseUrl(string url)
        => url.EndsWith("/", StringComparison.Ordinal) ? url.TrimEnd('/') : url;

    private static X509Certificate2 FindCertificateFromLocalMachineMy(string? thumbprint, string? subjectContains)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection matches;

        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            var normalized = thumbprint.Replace(" ", "", StringComparison.OrdinalIgnoreCase);
            matches = store.Certificates.Find(X509FindType.FindByThumbprint, normalized, validOnly: false);
        }
        else if (!string.IsNullOrWhiteSpace(subjectContains))
        {
            // Subject contains match (similar to your PS Where-Object {$_.Subject -match ...})
            matches = new X509Certificate2Collection(
                store.Certificates
                    .OfType<X509Certificate2>()
                    .Where(c => c.Subject.Contains(subjectContains, StringComparison.OrdinalIgnoreCase))
                    .ToArray());
        }
        else
        {
            throw new ArgumentException("Provide either certificateThumbprint or certificateSubjectContains.");
        }

        var cert = matches
            .OfType<X509Certificate2>()
            .OrderByDescending(c => c.NotAfter) // pick the newest if multiple
            .FirstOrDefault();

        if (cert == null)
            throw new InvalidOperationException("Client certificate not found in LocalMachine\\My.");

        if (!cert.HasPrivateKey)
            throw new InvalidOperationException("Certificate found, but it has no private key. Vault cert auth requires a cert with a private key.");

        return cert;
    }

    private static string ExtractVaultClientToken(string json)
    {
        using var doc = JsonDocument.Parse(json);

        // Expected: { "auth": { "client_token": "..." }, ... }
        if (doc.RootElement.TryGetProperty("auth", out var auth) &&
            auth.TryGetProperty("client_token", out var tokenEl) &&
            tokenEl.ValueKind == JsonValueKind.String)
        {
            var token = tokenEl.GetString();
            if (!string.IsNullOrWhiteSpace(token))
                return token!;
        }

        throw new InvalidOperationException("Could not find auth.client_token in Vault login response.");
    }

    private static string ExtractPassword(string json)
    {
        using var doc = JsonDocument.Parse(json);

        // Expected Vault KV v2 shape:
        // { "data": { "data": { "password": "..." } } }
        if (doc.RootElement.TryGetProperty("data", out var data1) &&
            data1.TryGetProperty("data", out var data2) &&
            data2.TryGetProperty("password", out var pwdEl) &&
            pwdEl.ValueKind == JsonValueKind.String)
        {
            var pwd = pwdEl.GetString();
            if (!string.IsNullOrWhiteSpace(pwd))
                return pwd!;
        }

        throw new InvalidOperationException("Could not find data.data.password in Vault secret response.");
    }
}







public static class VaultClientNet40
{
    public static string GetSecret(
        string vaultUrl,
        string vaultNamespace,
        string vaultCertName,
        string vaultSecretPath,
        string certificateSubjectContains)
    {
        // PowerShell equivalent:
        // [Net.ServicePointManager]::SecurityProtocol = Tls12
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        var cert = LoadCertificateFromLocalMachine(certificateSubjectContains);

        // -------------------------
        // 1) LOGIN (POST)
        // -------------------------
        var loginUrl = vaultUrl.TrimEnd('/') + "/v1/auth/cert/login";

        var loginBody = new
        {
            name = vaultCertName
        };

        string loginResponse = SendRequest(
            url: loginUrl,
            method: "POST",
            jsonBody: Serialize(loginBody),
            cert: cert,
            headers: new WebHeaderCollection
            {
                { "X-Vault-Namespace", vaultNamespace }
            }
        );

        var token = ExtractClientToken(loginResponse);

        // -------------------------
        // 2) READ SECRET (GET)
        // -------------------------
        var secretUrl = vaultUrl.TrimEnd('/') + "/v1/secret/data/" + vaultSecretPath.TrimStart('/');

        string secretResponse = SendRequest(
            url: secretUrl,
            method: "GET",
            jsonBody: null,
            cert: cert,
            headers: new WebHeaderCollection
            {
                { "X-Vault-Namespace", vaultNamespace },
                { "X-Vault-Token", token }
            }
        );

        var password = ExtractPassword(secretResponse);

        return "SECRET: " + password;
    }

    // --------------------------------------------------------
    // HTTP (HttpWebRequest – .NET 4.0 safe)
    // --------------------------------------------------------
    private static string SendRequest(
        string url,
        string method,
        string jsonBody,
        X509Certificate2 cert,
        WebHeaderCollection headers)
    {
        var request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = method;
        request.ContentType = "application/json";
        request.Accept = "application/json";
        request.ClientCertificates.Add(cert);

        if (headers != null)
            request.Headers.Add(headers);

        if (!string.IsNullOrEmpty(jsonBody))
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            request.ContentLength = bodyBytes.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(bodyBytes, 0, bodyBytes.Length);
            }
        }

        try
        {
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }
        catch (WebException ex)
        {
            using (var reader = new StreamReader(ex.Response.GetResponseStream()))
            {
                var error = reader.ReadToEnd();
                throw new InvalidOperationException("Vault call failed:\n" + error, ex);
            }
        }
    }

    // --------------------------------------------------------
    // CERTIFICATE (LocalMachine\My)
    // --------------------------------------------------------
    private static X509Certificate2 LoadCertificateFromLocalMachine(string subjectContains)
    {
        var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);

        var cert = store.Certificates
            .Cast<X509Certificate2>()
            .Where(c => c.Subject.IndexOf(subjectContains, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderByDescending(c => c.NotAfter)
            .FirstOrDefault();

        store.Close();

        if (cert == null)
            throw new InvalidOperationException("Client certificate not found.");

        if (!cert.HasPrivateKey)
            throw new InvalidOperationException("Certificate has no private key.");

        return cert;
    }

    // --------------------------------------------------------
    // JSON helpers (JavaScriptSerializer)
    // --------------------------------------------------------
    private static string Serialize(object obj)
    {
        return new JavaScriptSerializer().Serialize(obj);
    }

    private static string ExtractClientToken(string json)
    {
        var serializer = new JavaScriptSerializer();
        dynamic data = serializer.DeserializeObject(json);

        // auth.client_token
        return data["auth"]["client_token"];
    }

    private static string ExtractPassword(string json)
    {
        var serializer = new JavaScriptSerializer();
        dynamic data = serializer.DeserializeObject(json);

        // data.data.password
        return data["data"]["data"]["password"];
    }
}

