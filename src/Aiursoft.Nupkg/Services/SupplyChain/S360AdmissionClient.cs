using Aiursoft.Nupkg.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Aiursoft.Nupkg.Services.SupplyChain;

public class S360AdmissionClient(IHttpClientFactory clients, IOptions<S360MirrorOptions> options)
{
    public bool UsesRegistryPolicy => !string.IsNullOrEmpty(options.Value.PolicySource);
    public string PolicySource(string origin) => UsesRegistryPolicy ? options.Value.PolicySource.TrimEnd('/') : origin;

    public async Task<List<ArtifactIdentity>> CandidatesAsync(string source, string name,
        IEnumerable<string> versions, CancellationToken token)
    {
        var result = new List<ArtifactIdentity>();
        foreach (var batch in versions.Distinct().Chunk(500))
        {
            var candidates = batch.Select(v => new MirrorCandidate("nuget", name, v, PolicySource(source))).ToArray();
            using var request = Create(HttpMethod.Post, "api/artifacts/mirror/candidates");
            request.Content = JsonContent.Create(new { candidates });
            using var response = await clients.CreateClient("S360Admission").SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            var decisions = (await response.Content.ReadFromJsonAsync<MirrorCandidateResponse>(token))?.Decisions;
            if (decisions == null || decisions.Count != batch.Length ||
                decisions.Where((d, i) => d.Candidate != candidates[i]).Any())
                throw new InvalidDataException("S360 returned unrelated mirror candidates.");
            result.AddRange(decisions.Where(d => d.CanFetch).Select(d => new ArtifactIdentity(
                "nuget", name, d.Candidate.Version, PolicySource(source), string.Empty)));
        }
        return result;
    }

    public async Task<ArtifactDecision> VerifyMirrorAsync(ArtifactIdentity artifact, CancellationToken token)
    {
        using var request = Create(HttpMethod.Post, "api/artifacts/mirror/verify");
        request.Content = JsonContent.Create(artifact);
        using var response = await clients.CreateClient("S360Admission").SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        var decision = await response.Content.ReadFromJsonAsync<ArtifactDecision>(token);
        if (decision == null || decision.Dependency != artifact)
            throw new InvalidDataException("S360 returned an unrelated mirror verification.");
        return decision;
    }

    private HttpRequestMessage Create(HttpMethod method, string path)
    {
        var settings = options.Value;
        var request = new HttpRequestMessage(method, new Uri(settings.Server.AbsoluteUri.TrimEnd('/') + "/" + path));
        request.Headers.Add("X-S360-Key", settings.CheckKey);
        return request;
    }

    public async Task<List<ArtifactDecision>> AssessAsync(IReadOnlyList<ArtifactIdentity> artifacts, CancellationToken cancellationToken)
    {
        var decisions = new List<ArtifactDecision>();
        foreach (var batch in artifacts.Chunk(500))
        {
            using var request = Create(HttpMethod.Post, "api/artifacts/assess");
            request.Content = JsonContent.Create(new { dependencies = batch });
            using var response = await clients.CreateClient("S360Admission").SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ArtifactAssessmentResponse>(cancellationToken)
                ?? throw new InvalidDataException("S360 returned an empty assessment.");
            if (result.Decisions.Count != batch.Length || result.Decisions.Where((d, i) => d.Dependency != batch[i]).Any())
                throw new InvalidDataException("S360 assessment does not match the requested artifacts.");
            decisions.AddRange(result.Decisions);
        }
        return decisions;
    }

    public async IAsyncEnumerable<ArtifactIdentity> ApprovedAsync(string source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken, string? name = null)
    {
        int after = 0;
        int? upper = null;
        do
        {
            var path = $"api/artifacts/approved?ecosystem=nuget&source={Uri.EscapeDataString(source)}&afterId={after}&take=100";
            if (name != null) path += "&name=" + Uri.EscapeDataString(name);
            if (upper.HasValue) path += $"&upperId={upper.Value}";
            using var request = Create(HttpMethod.Get, path);
            using var response = await clients.CreateClient("S360Admission").SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<ApprovedArtifactPage>(cancellationToken)
                ?? throw new InvalidDataException("S360 returned an empty artifact page.");
            if (page.Artifacts.Count > 100 || page.UpperId < after ||
                (upper.HasValue && upper != page.UpperId) ||
                (page.NextAfterId.HasValue && (page.NextAfterId <= after || page.NextAfterId > page.UpperId)))
                throw new InvalidDataException("S360 returned an invalid artifact cursor.");
            upper = page.UpperId;
            foreach (var artifact in page.Artifacts) yield return artifact;
            if (!page.NextAfterId.HasValue) yield break;
            after = page.NextAfterId.Value;
        } while (true);
    }
}
