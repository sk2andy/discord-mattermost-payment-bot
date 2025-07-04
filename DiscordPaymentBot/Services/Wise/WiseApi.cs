using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using discord_payment_bot.Models.Wise;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using Jose;
using System;
using System.Collections.Generic;

namespace discord_payment_bot.Services.Wise;

public class WiseApi : IPaymentApi
{
    private readonly HttpClient _httpClient;
    private readonly WiseOptions _options;

    public WiseApi(HttpClient httpClient, IOptions<WiseOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task<List<Transaction>> GetIncomeTransactionsAsync(DateTimeOffset since, decimal minAmount, string requestCurrency = "USD", CancellationToken cancellationToken = default)
    {
        // Retrieve list of STANDARD balance accounts
        var balancesUrl = $"/v4/profiles/{_options.ProfileId}/balances?types=STANDARD";
        var balancesResp = await _httpClient.GetAsync(balancesUrl, cancellationToken);
        balancesResp.EnsureSuccessStatusCode();
        var balancesJson = await balancesResp.Content.ReadAsStringAsync(cancellationToken);
        var balancesRoot = JsonDocument.Parse(balancesJson).RootElement;
        var balanceId = balancesRoot[0].GetProperty("id").GetInt64();

        // Fetch statement for the given period
        var end = DateTime.UtcNow;
        var stmtUrl = $"/v1/profiles/{_options.ProfileId}/balance-statements/{balanceId}/statement.json?intervalStart={since:O}&intervalEnd={end:O}&type=COMPACT&currency={requestCurrency}";
        var stmtResp = await _httpClient.GetAsync(stmtUrl, cancellationToken);
        stmtResp.EnsureSuccessStatusCode();
        var stmtJson = await stmtResp.Content.ReadAsStringAsync(cancellationToken);
        var stmtRoot = JsonDocument.Parse(stmtJson).RootElement;

        var result = new List<Transaction>();
        foreach (var tx in stmtRoot.GetProperty("transactions").EnumerateArray())
        {
            var amount = tx.GetProperty("amount").GetProperty("value").GetDecimal();
            if (amount < minAmount) continue;
            var reference = tx.GetProperty("referenceNumber").GetString();
            var paymentReference = tx.GetProperty("details").GetProperty("paymentReference").GetString();
            var paymentDate = tx.GetProperty("date").GetDateTime();
            var referenceNumber = tx.GetProperty("referenceNumber").GetString();
            var currency = tx.GetProperty("amount").GetProperty("currency").GetString();
            var senderAccount = tx.GetProperty("details").GetProperty("senderAccount").GetString();
            result.Add(new Transaction
            {
                Amount = amount,
                Reference = reference,
                PaymentReference = paymentReference,
                PaymentDate = paymentDate,
                Currency = currency ?? string.Empty,
                SenderAccount = senderAccount,
                TransactionId = referenceNumber
            });
        }
        return result;
    }

    public async Task<string> SendMoneyBackAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        // 1. Estimate the fee by quoting the full amount
        var tempQuoteRequest = new
        {
            profile = _options.ProfileId,
            sourceCurrency = transaction.Currency,
            targetCurrency = transaction.Currency,
            sourceAmount = transaction.Amount
        };
        var tempQuoteContent = new StringContent(JsonSerializer.Serialize(tempQuoteRequest), Encoding.UTF8, "application/json");
        var tempQuoteResp = await _httpClient.PostAsync($"/v3/profiles/{_options.ProfileId}/quotes", tempQuoteContent, cancellationToken);
        tempQuoteResp.EnsureSuccessStatusCode();
        var tempQuoteJson = await tempQuoteResp.Content.ReadAsStringAsync(cancellationToken);
        var tempQuoteRoot = JsonDocument.Parse(tempQuoteJson).RootElement;
        var fee = tempQuoteRoot.GetProperty("fee").GetProperty("amount").GetDecimal();

        // 2. Calculate net amount so sender pays exactly transaction.Amount
        var netAmount = transaction.Amount - fee;

        // 3. Create final quote for the net amount
        var quoteRequest = new
        {
            profile = _options.ProfileId,
            sourceCurrency = transaction.Currency,
            targetCurrency = transaction.Currency,
            sourceAmount = netAmount
        };
        var quoteContent = new StringContent(JsonSerializer.Serialize(quoteRequest), Encoding.UTF8, "application/json");
        var quoteResp = await _httpClient.PostAsync($"/v3/profiles/{_options.ProfileId}/quotes", quoteContent, cancellationToken);
        quoteResp.EnsureSuccessStatusCode();
        var quoteJson = await quoteResp.Content.ReadAsStringAsync(cancellationToken);
        var quoteRoot = JsonDocument.Parse(quoteJson).RootElement;
        var quoteId = quoteRoot.GetProperty("id").GetString();

        // 4. Create (or reuse) recipient with IBAN
        var recipientRequest = new
        {
            currency = transaction.Currency,
            type = "iban",
            profile = _options.ProfileId,
            accountHolderName = transaction.Reference,
            legalType = "PRIVATE",
            iban = transaction.SenderAccount
        };
        var recipientContent = new StringContent(JsonSerializer.Serialize(recipientRequest), Encoding.UTF8, "application/json");
        var recipientResp = await _httpClient.PostAsync("/v1/accounts", recipientContent, cancellationToken);
        recipientResp.EnsureSuccessStatusCode();
        var recipientJson = await recipientResp.Content.ReadAsStringAsync(cancellationToken);
        var recipientRoot = JsonDocument.Parse(recipientJson).RootElement;
        var recipientId = recipientRoot.GetProperty("id").GetString();

        // 5. Create the transfer using the net quote
        var transferRequest = new
        {
            targetAccount = recipientId,
            quoteUuid = quoteId,
            customerTransactionId = transaction.TransactionId
        };
        var transferContent = new StringContent(JsonSerializer.Serialize(transferRequest), Encoding.UTF8, "application/json");
        var transferResp = await _httpClient.PostAsync("/v1/transfers", transferContent, cancellationToken);
        transferResp.EnsureSuccessStatusCode();
        var transferJson = await transferResp.Content.ReadAsStringAsync(cancellationToken);
        var transferRoot = JsonDocument.Parse(transferJson).RootElement;
        var transferId = transferRoot.GetProperty("id").GetString();

        // 6. Fund the transfer from your Wise balance
        var paymentRequest = new { type = "BALANCE" };
        var paymentContent = new StringContent(JsonSerializer.Serialize(paymentRequest), Encoding.UTF8, "application/json");
        var paymentUrl = $"/v3/profiles/{_options.ProfileId}/transfers/{transferId}/payments";
        var paymentResp = await _httpClient.PostAsync(paymentUrl, paymentContent, cancellationToken);
        paymentResp.EnsureSuccessStatusCode();

        return transferId;
    }
    
    private async Task<JsonDocument> GetOneTimeTokenForRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        // First attempt: may return 403 with One-Time-Token header
        var initialResponse = await _httpClient.SendAsync(request, cancellationToken);
        if (initialResponse.StatusCode == HttpStatusCode.Forbidden &&
            initialResponse.Headers.TryGetValues("x-2fa-approval", out var ottValues))
        {
            // Extract token and retry with SCA header
            var token = ottValues.First();
            request.Headers.Add("One-Time-Token", token);
            var finalResponse = await _httpClient.SendAsync(request, cancellationToken);
            finalResponse.EnsureSuccessStatusCode();
            var json = await finalResponse.Content.ReadAsStringAsync(cancellationToken);
            return JsonDocument.Parse(json);
        }
        // No SCA needed or token already provided
        initialResponse.EnsureSuccessStatusCode();
        var content = await initialResponse.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(content);
    }

    // public async Task CreatePinAsync(CancellationToken cancellationToken = default)
    // {
    //     // Step 1: Register client public key for JWE enhancements
    //     using var clientRsa = RSA.Create(2048);
    //     var clientPublicKey = Convert.ToBase64String(clientRsa.ExportSubjectPublicKeyInfo());
    //     var registerKeyId = Guid.NewGuid().ToString();
    //     var registerKeyRequest = new
    //     {
    //         keyId = registerKeyId,
    //         scope = "PAYLOAD_ENCRYPTION",
    //         validFrom = DateTime.UtcNow.ToString("o"),
    //         validTill = DateTime.UtcNow.AddYears(1).ToString("o"),
    //         publicKeyMaterial = new
    //         {
    //             algorithm = "RSA_OAEP_256",
    //             keyMaterial = clientPublicKey
    //         }
    //     };
    //     var registerContent = new StringContent(JsonSerializer.Serialize(registerKeyRequest), Encoding.UTF8, "application/json");
    //     var registerResponse = await _httpClient.PostAsync("/v1/auth/jose/request/public-keys", registerContent, cancellationToken);
    //     registerResponse.EnsureSuccessStatusCode();
    //     var registerJson = await registerResponse.Content.ReadAsStringAsync(cancellationToken);
    //     // Capture the returned keyId for use in JWE header
    //     var registerRoot = JsonDocument.Parse(registerJson).RootElement;
    //     var clientKeyId = registerRoot.GetProperty("keyId").GetString();
    //
    //     var joseKeyResponse = await _httpClient.GetAsync(
    //         "/v1/auth/jose/response/public-keys?algorithm=RSA_OAEP_256&scope=PAYLOAD_ENCRYPTION", cancellationToken);
    //     joseKeyResponse.EnsureSuccessStatusCode();
    //     var keyResponseJson = JsonSerializer.Deserialize<JsonDocument>(await joseKeyResponse.Content.ReadAsStringAsync(cancellationToken));
    //     var keyMaterial = keyResponseJson?.RootElement.GetProperty("keyMaterial").GetProperty("keyMaterial").GetString()
    //         ?? throw new InvalidOperationException("Key material not found in response");
    //     
    //     var blob = Convert.FromBase64String(keyMaterial);
    //     using var rsa = RSA.Create();
    //     rsa.ImportSubjectPublicKeyInfo(blob, out _);
    //     var extraHeaders = new Dictionary<string, object> { { "kid", clientKeyId } };
    //     var jwe = Jose.JWT.Encode(
    //         _options.WiseCommunicationPin,
    //         rsa,
    //         JweAlgorithm.RSA_OAEP_256,
    //         JweEncryption.A256GCM,
    //         extraHeaders: extraHeaders
    //     );
    //     
    //     // 4. Prepare and send the Create PIN request
    //     var request = new HttpRequestMessage(HttpMethod.Post, "/v1/user/pin");
    //     request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jose+json"));
    //     request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
    //     request.Headers.Add("X-TW-JOSE-Method", "jwe");
    //     // Build content with JWE and explicit identity encoding
    //     request.Content = new StringContent(jwe, Encoding.UTF8, "application/jose+json");
    //     request.Content.Headers.ContentEncoding.Add("identity");
    //
    //     // 5. Send and validate
    //     using var response = await _httpClient.SendAsync(request, cancellationToken);
    //     response.EnsureSuccessStatusCode();
    // }
    
    public async Task<string> CreateDeviceFingerprintAsync(CancellationToken cancellationToken = default)
    {
        const string requestUrl = "/v1/user/partner-device-fingerprints";
        // Build JSON payload
        var body = new { _options.DeviceFingerPrint };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/jose+json");
        // Set JOSE content type
        content.Headers.ContentType = new MediaTypeHeaderValue("application/jose+json");

        // Prepare HTTP request with JOSE headers
        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = content
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jose+json"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
        request.Headers.Add("X-TW-JOSE-Method", "jwe");

        // Send request
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Parse response
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var root = JsonDocument.Parse(json).RootElement;
        return root.GetProperty("deviceFingerprintId").GetString() ?? string.Empty;
    }
}