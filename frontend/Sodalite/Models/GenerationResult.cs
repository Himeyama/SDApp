namespace Sodalite.Models;

sealed record GenerationResult(string JobId, string Status, string? ImageUrl, string? Error);
