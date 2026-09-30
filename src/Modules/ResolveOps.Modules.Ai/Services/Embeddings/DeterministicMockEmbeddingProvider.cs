using System.Security.Cryptography;
using System.Text;

namespace ResolveOps.Modules.Ai.Services.Embeddings;

/// <summary>
/// Deterministic mock vector embedding generator for offline testing, CI evaluation, and local benchmarks.
/// Produces a unit-normalized 64-dimensional float vector using token frequency and hashing.
/// </summary>
public sealed class DeterministicMockEmbeddingProvider : IEmbeddingService
{
    private const int _dimensions = 64;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            var empty = new float[_dimensions];
            empty[0] = 1.0f;
            return Task.FromResult(empty);
        }

        var vector = new float[_dimensions];
        var words = text.ToLowerInvariant().Split([' ', '\r', '\n', '\t', ',', '.', ';', ':', '!', '?', '-', '_', '(', ')', '[', ']', '{', '}'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(word));
            for (var i = 0; i < _dimensions; i++)
            {
                var bucket = (i * 4) % hashBytes.Length;
                var val = (sbyte)hashBytes[bucket];
                vector[i] += val / 128.0f;
            }
        }

        // L2 Normalization (unit length so cosine similarity = dot product)
        var sumSquares = 0.0f;
        for (var i = 0; i < _dimensions; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        var magnitude = MathF.Sqrt(sumSquares);
        if (magnitude > 1e-6f)
        {
            for (var i = 0; i < _dimensions; i++)
            {
                vector[i] /= magnitude;
            }
        }
        else
        {
            vector[0] = 1.0f;
        }

        return Task.FromResult(vector);
    }
}
