namespace Aiursoft.Nupkg.Services.SupplyChain;

public record ArtifactIdentity(string Ecosystem, string Name, string Version, string Source, string Sha256);
public class ArtifactDecision
{
    public ArtifactIdentity Dependency { get; set; } = null!;
    public bool Trusted { get; set; }
    public bool PendingVerification { get; set; }
    public string Reason { get; set; } = string.Empty;
}
public class ArtifactAssessmentResponse
{
    public List<ArtifactDecision> Decisions { get; set; } = [];
}
public class ApprovedArtifactPage
{
    public List<ArtifactIdentity> Artifacts { get; set; } = [];
    public int? NextAfterId { get; set; }
    public int UpperId { get; set; }
}

public record MirrorCandidate(string Ecosystem, string Name, string Version, string Source);
public record MirrorCandidateDecision(MirrorCandidate Candidate, bool CanFetch);
public record MirrorCandidateResponse(List<MirrorCandidateDecision> Decisions);

public record PackageMirrorCandidate(string Name, string Version, string? Sha256);
