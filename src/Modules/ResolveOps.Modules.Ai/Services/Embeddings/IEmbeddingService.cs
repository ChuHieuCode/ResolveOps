namespace ResolveOps.Modules.Ai.Services.Embeddings;

/// <summary>
/// Abstraction for text vector embeddings generation.
/// Master Spec §6.3, §19.8.
/// </summary>
public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
