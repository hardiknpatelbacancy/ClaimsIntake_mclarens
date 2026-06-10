using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClaimsIntake.Tests.Integration;

public class ClaimsApiTests : IClassFixture<ClaimsApiFactory>
{
    private readonly HttpClient _client;

    public ClaimsApiTests(ClaimsApiFactory factory)
    {
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    private static object ValidPayload(string policyNumber) => new
    {
        policyNumber,
        claimantName = "Jane Doe",
        description = "Rear-end collision on I-95",
        incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)).ToString("yyyy-MM-dd")
    };

    private async Task<Guid> SubmitClaimAsync(string policyNumber)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/claims", ValidPayload(policyNumber));
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetGuid();
    }

    private async Task PatchStatusAsync(Guid id, string status, string? reviewNotes = null)
    {
        var response = await _client.PatchAsJsonAsync($"/api/v1/claims/{id}/status", new { status, reviewNotes });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PostValidClaim_Returns201WithLocationHeader()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/claims", ValidPayload("POL-9001"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Submitted", body.RootElement.GetProperty("status").GetString());

        Assert.NotNull(response.Headers.Location);
        Assert.True(response.Headers.Location!.IsAbsoluteUri); // CreatedAtRoute emits an absolute URL
        Assert.EndsWith($"/api/v1/claims/{id}", response.Headers.Location.AbsolutePath);

        // The Location URL must dereference to the created claim.
        var follow = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, follow.StatusCode);
    }

    [Fact]
    public async Task PostMissingPolicyNumber_Returns422WithCamelCaseErrorKey()
    {
        var payload = new
        {
            claimantName = "Jane Doe",
            description = "desc",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd")
        };

        var response = await _client.PostAsJsonAsync("/api/v1/claims", payload);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("https://tools.ietf.org/html/rfc4918#section-11.2", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("Validation Failed", body.RootElement.GetProperty("title").GetString());
        Assert.Equal(422, body.RootElement.GetProperty("status").GetInt32());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("policyNumber", out _),
            "errors dictionary must contain camelCase key 'policyNumber'");
    }

    [Fact]
    public async Task PostFutureIncidentDate_Returns422()
    {
        var payload = new
        {
            policyNumber = "POL-9002",
            claimantName = "Jane Doe",
            description = "desc",
            incidentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).ToString("yyyy-MM-dd")
        };

        var response = await _client.PostAsJsonAsync("/api/v1/claims", payload);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("incidentDate", out _));
    }

    [Fact]
    public async Task GetClaims_FiltersByStatus()
    {
        // Distinct policy number isolates this test from data created by the others.
        var policyNumber = "POL-9100";
        var submittedId = await SubmitClaimAsync(policyNumber);
        var underReviewId = await SubmitClaimAsync(policyNumber);
        await PatchStatusAsync(underReviewId, "UnderReview");

        var response = await _client.GetAsync($"/api/v1/claims?status=UnderReview&policyNumber={policyNumber}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = body.RootElement.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(underReviewId, ids);
        Assert.DoesNotContain(submittedId, ids);
        Assert.All(body.RootElement.EnumerateArray(), c => Assert.Equal("UnderReview", c.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task GetClaims_PageSizeOverMax_Returns422()
    {
        var response = await _client.GetAsync("/api/v1/claims?pageSize=101");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task PatchIllegalTransition_Returns409()
    {
        var id = await SubmitClaimAsync("POL-9200");

        // Submitted → Approved is not a legal transition.
        var response = await _client.PatchAsJsonAsync($"/api/v1/claims/{id}/status", new { status = "Approved" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(409, body.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("cannot transition", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task PatchDeniedWithoutNotes_Returns422()
    {
        var id = await SubmitClaimAsync("POL-9300");
        await PatchStatusAsync(id, "UnderReview"); // Denied is only reachable from UnderReview

        var response = await _client.PatchAsJsonAsync($"/api/v1/claims/{id}/status", new { status = "Denied" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("reviewNotes", out _));
    }

    [Fact]
    public async Task PatchDeniedWithNotes_Returns200()
    {
        var id = await SubmitClaimAsync("POL-9400");
        await PatchStatusAsync(id, "UnderReview");

        var response = await _client.PatchAsJsonAsync(
            $"/api/v1/claims/{id}/status", new { status = "Denied", reviewNotes = "Policy lapsed before incident." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Denied", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("Policy lapsed before incident.", body.RootElement.GetProperty("reviewNotes").GetString());
    }

    [Fact]
    public async Task GetUnknownClaim_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync($"/api/v1/claims/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Resource Not Found", body.RootElement.GetProperty("title").GetString());
    }
}
