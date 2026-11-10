using Aiursoft.Nupkg.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Aiursoft.Nupkg.Services.SupplyChain;

public class S360AdmissionClient(IHttpClientFactory clients, IOptions<S360MirrorOptions> options)
{
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
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int after = 0;
        int? upper = null;
        do
        {
            var path = $"api/artifacts/approved?ecosystem=nuget&source={Uri.EscapeDataString(source)}&afterId={after}&take=100";
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
