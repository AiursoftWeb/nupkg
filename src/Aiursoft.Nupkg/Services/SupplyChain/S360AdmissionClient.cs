using Aiursoft.Nupkg.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Aiursoft.Nupkg.Services.SupplyChain;

public class S360AdmissionClient(IHttpClientFactory clients, IOptions<S360MirrorOptions> options)
{
    public bool UsesRegistryPolicy => !string.IsNullOrEmpty(options.Value.PolicySource);
    public string PolicySource(string origin) => UsesRegistryPolicy ? options.Value.PolicySource.TrimEnd('/') : origin;

    public async Task<List<MirrorCandidate>> CandidatesAsync(string source, string name,
        IEnumerable<string> versions, CancellationToken token)
    {
        var result = new List<MirrorCandidate>();
        foreach (var batch in versions.Distinct().Chunk(500))
        {
            var candidates = batch.Select(v => new MirrorCandidate("nuget", name, v, PolicySource(source))).ToArray();
            var decisions = await AssessAsync(candidates.Select(c =>
                new ArtifactIdentity(c.Ecosystem, c.Name, c.Version, c.Source, "")).ToList(), token);
            result.AddRange(decisions.Where(d => d.Trusted).Select(d =>
                new MirrorCandidate(d.Dependency.Ecosystem, d.Dependency.Name, d.Dependency.Version, d.Dependency.Source)));
        }
        return result;
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
            request.Content = JsonContent.Create(new { dependencies = batch.Select(a => new { a.Ecosystem, a.Name, a.Version, a.Source }) });
            using var response = await clients.CreateClient("S360Admission").SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ArtifactAssessmentResponse>(cancellationToken)
                ?? throw new InvalidDataException("S360 returned an empty assessment.");
            if (result.Decisions == null || result.Decisions.Count != batch.Length ||
                result.Decisions.Cast<ArtifactDecision?>().Where((d, i) => d?.Dependency is not { } dependency ||
                    dependency with { Sha256 = batch[i].Sha256 } != batch[i]).Any())
                throw new InvalidDataException("S360 assessment does not match the requested artifacts.");
            if (result.Decisions.Any(d => d.PendingVerification))
                throw new HttpRequestException("S360 vulnerability data is unavailable.");
            for (var i = 0; i < batch.Length; i++) result.Decisions[i].Dependency = batch[i];
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
