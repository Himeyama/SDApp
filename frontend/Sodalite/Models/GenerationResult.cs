namespace Sodalite.Models;

sealed record GenerationResult(
    string JobId,
    string Status,
    int ImagesCompleted,
    int TotalImages,
    string? ImageUrl,
    string? ImagePath,
    string? Error);
