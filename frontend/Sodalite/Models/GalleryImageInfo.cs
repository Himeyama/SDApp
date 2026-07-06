namespace Sodalite.Models;

sealed record GalleryImageInfo(
    string ImageId,
    string ImageUrl,
    string ImagePath,
    double CreatedAt,
    GalleryParameters? Parameters);

sealed record GalleryParameters(
    string Prompt,
    string NegativePrompt,
    int? Steps,
    double? CfgScale,
    int? Width,
    int? Height,
    string? Sampler,
    long? Seed,
    IReadOnlyList<LoraSelection> Loras);
