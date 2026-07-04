"""Discovers text-to-image models available in the local Hugging Face cache."""

from huggingface_hub import scan_cache_dir
from huggingface_hub.utils import CachedRepoInfo

from sdapp_backend.schemas.generation import ModelInfo


def list_cached_models(active_model_id: str) -> list[ModelInfo]:
    """List locally cached diffusers pipeline repos, flagging the currently active one."""
    cache_info = scan_cache_dir()
    models = [
        ModelInfo(
            model_id=repo.repo_id,
            is_active=repo.repo_id == active_model_id,
            size_on_disk_bytes=repo.size_on_disk,
        )
        for repo in cache_info.repos
        if repo.repo_type == "model" and _has_pipeline_files(repo)
    ]
    if not any(model.is_active for model in models):
        models.append(ModelInfo(model_id=active_model_id, is_active=True, size_on_disk_bytes=0))
    return sorted(models, key=lambda model: model.model_id)


def _has_pipeline_files(repo: CachedRepoInfo) -> bool:
    file_names = {file.file_name for revision in repo.revisions for file in revision.files}
    return "model_index.json" in file_names
