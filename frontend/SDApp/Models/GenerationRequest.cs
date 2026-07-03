namespace SDApp.Models;

sealed record GenerationRequest(string Prompt, string NegativePrompt = "", int Steps = 20);
