namespace Sodalite.Models;

sealed record GenerationResult(string JobId, string Status, string? ImageUrl, string? ImagePath, string? Error);
