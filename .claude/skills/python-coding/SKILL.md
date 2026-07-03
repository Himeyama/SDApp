---
name: python-coding
description: Python 3.13+ のコーディング規約。uv・型ヒント・dataclass・async (TaskGroup)・pytest など。
---

# Python Coding Guidelines

Target: Python 3.13+.

## Project Management

Use `uv` for everything: packages, virtualenvs, and running scripts.

```bash
uv init myproject          # create project
uv add fastapi             # add dependency
uv add --dev pytest ruff   # add dev dependency
uv run python main.py      # run script
uv run pytest              # run tests
uvx ruff check .           # run a tool without installing
```

### pyproject.toml

```toml
[project]
name = "myproject"
version = "0.1.0"
requires-python = ">=3.13"
dependencies = ["fastapi>=0.115"]

[dependency-groups]
dev = ["pytest>=8", "ruff>=0.9"]

[tool.ruff]
target-version = "py313"
line-length = 100

[tool.ruff.lint]
select = ["E", "F", "I", "UP", "B", "SIM"]

[tool.pytest.ini_options]
testpaths = ["tests"]
```

## Type Hints

Use built-in generics and `X | None`. No `from __future__ import annotations` needed in 3.13.

```python
def divide(a: float, b: float) -> float | None:
    return a / b if b != 0 else None

def process(items: list[str]) -> dict[str, int]:
    return {item: len(item) for item in items}
```

- Use `str | None`, never `Optional[str]`.
- Prefer abstract argument types from `collections.abc` (`Iterable`, `Mapping`, `Sequence`) over concrete `list`/`dict` — more flexible for callers.

```python
from collections.abc import Iterable, Mapping, Sequence

def process(items: Iterable[str]) -> None: ...        # accepts list, tuple, generator
def configure(options: Mapping[str, str]) -> None: ... # read-only dict-like
def ends(items: Sequence[int]) -> tuple[int, int]:     # needs indexing
    return items[0], items[-1]
```

### `type` aliases (3.12+)

```python
from collections.abc import Callable

type UserId = int
type Point = tuple[float, float]
type Callback[T] = Callable[[T], None]
```

### Generics (3.12+ syntax)

```python
def first[T](items: list[T]) -> T | None:
    return items[0] if items else None

class Stack[T]:
    def __init__(self) -> None:
        self._items: list[T] = []

    def push(self, item: T) -> None:
        self._items.append(item)

    def pop(self) -> T:
        return self._items.pop()
```

### `Literal`, `Self`, `Protocol`

```python
from typing import Literal, Protocol, Self

Mode = Literal["read", "write", "append"]  # restrict to known values

class Builder:
    def set_name(self, name: str) -> Self:  # method chaining
        self.name = name
        return self

class Reader(Protocol):  # duck-typed interface, no inheritance required
    def read(self) -> str: ...
```

## Docstrings

Use `"""triple double quotes"""`. Write a docstring for public modules, classes, and functions; skip it for trivial or private helpers.

- Start with a one-line summary in the imperative mood, ending with a period: `"""Fetch JSON from a URL."""`.
- Don't restate what type hints already say. Document only the non-obvious: edge cases, side effects, raised exceptions, units.
- For multi-line, put the summary on the first line, a blank line, then details.

```python
def divide(a: float, b: float) -> float | None:
    """Divide a by b, returning None if b is zero."""
    return a / b if b != 0 else None


def fetch_all(urls: list[str], *, retries: int = 3) -> list[dict]:
    """Fetch every URL concurrently.

    Retries each request up to `retries` times on network errors.
    Raises ExceptionGroup if any URL still fails afterward.
    """
    ...
```

## Data Definitions

### dataclass

Use `frozen=True` for immutable data, `slots=True` when memory matters.

```python
from dataclasses import dataclass, field

@dataclass(frozen=True, slots=True)
class User:
    id: int
    name: str
    tags: tuple[str, ...] = ()

@dataclass
class Cart:
    items: list[str] = field(default_factory=list)  # mutable default
```

### TypedDict / Enum

```python
from enum import StrEnum, auto
from typing import NotRequired, TypedDict

class Config(TypedDict):
    host: str
    port: int
    debug: NotRequired[bool]  # optional key

class Status(StrEnum):
    ACTIVE = auto()    # value is "active"
    INACTIVE = auto()
```

## Pattern Matching

```python
match command:
    case "quit":
        raise SystemExit
    case str(s) if s.startswith("/"):
        run_slash_command(s)
    case _:
        print(f"Unknown: {command}")

match response:
    case {"status": 200, "data": data}:
        process(data)
    case {"status": int(code)} if code >= 500:
        raise ServerError(code)
```

## Collections

```python
squares = [x**2 for x in range(10) if x % 2 == 0]   # list comprehension
lengths = {word: len(word) for word in words}        # dict comprehension
total = sum(x**2 for x in range(1_000_000))          # generator, memory-efficient
```

```python
from itertools import islice, chain, groupby

first_five = list(islice(items, 5))
merged = list(chain(list_a, list_b))

# groupby only groups *adjacent* equal keys — sort first
for key, group in groupby(sorted(items, key=keyfn), key=keyfn):
    print(key, list(group))
```

## Async

```python
import asyncio
from typing import Any
import httpx

async def fetch(url: str) -> dict[str, Any]:
    async with httpx.AsyncClient() as client:
        response = await client.get(url)
        response.raise_for_status()
        return response.json()

# TaskGroup (3.11+): structured concurrency, both done after the block
async def main() -> None:
    async with asyncio.TaskGroup() as tg:
        a = tg.create_task(fetch("https://api.example.com/a"))
        b = tg.create_task(fetch("https://api.example.com/b"))
    print(a.result(), b.result())
```

## Error Handling

Don't use exceptions for normal control flow.

```python
value = d.get("key", default)   # not try/except KeyError

# try/except is fine for genuine conversion failures
try:
    value = int(s)
except ValueError:
    value = 0
```

```python
# Exception groups (3.11+)
try:
    async with asyncio.TaskGroup() as tg:
        tg.create_task(task_a())
        tg.create_task(task_b())
except* ValueError as eg:
    for exc in eg.exceptions:
        print(exc)

# Custom exceptions: subclass a single app-level base
class AppError(Exception): ...

class NotFoundError(AppError):
    def __init__(self, resource: str, id: int) -> None:
        super().__init__(f"{resource} not found: {id}")
        self.resource, self.id = resource, id
```

## Context Managers

```python
import time
from collections.abc import Iterator
from contextlib import contextmanager

@contextmanager
def timer(label: str) -> Iterator[None]:
    start = time.perf_counter()
    try:
        yield
    finally:
        print(f"{label}: {time.perf_counter() - start:.3f}s")
```

## Paths

Use `pathlib.Path`, not `os.path`.

```python
from pathlib import Path

path = Path("data") / "config.json"
if path.exists():
    text = path.read_text(encoding="utf-8")

Path("output.txt").write_text("hello", encoding="utf-8")
Path("src").mkdir(parents=True, exist_ok=True)

for py in Path(".").rglob("*.py"):
    print(py)

path.name    # "config.json"
path.stem    # "config"
path.suffix  # ".json"
path.parent  # Path("data")
```

## Lint & Format

Use Ruff for both.

```bash
uv run ruff check --fix .
uv run ruff format .
```

```toml
[tool.ruff.lint]
select = ["E", "F", "I", "UP", "B", "SIM", "RUF"]
ignore = ["E501"]  # line length handled by formatter
```

## Tests

Use pytest.

```python
import pytest

@pytest.mark.parametrize("name,expected", [
    ("Hikari", True),
    ("", False),
    (None, False),
])
def test_is_valid_name(name: str | None, expected: bool):
    assert is_valid_name(name) == expected

@pytest.fixture
def sample_user() -> User:
    return User(id=1, name="Hikari")

# async tests need pytest-asyncio
@pytest.mark.asyncio
async def test_fetch():
    result = await fetch("https://api.example.com")
    assert "data" in result
```

## Misc

```python
# f-strings (3.12+)
print(f"{name=}")       # name='Hikari'  (debug)
print(f"{value:.2f}")   # 3.14

# walrus operator
if data := fetch():
    process(data)
while chunk := file.read(8192):
    process(chunk)
```

## Naming

| Target | Convention | Example |
|---|---|---|
| Module / package | `snake_case` | `user_service.py` |
| Class | `PascalCase` | `UserService` |
| Function / variable | `snake_case` | `get_user`, `is_active` |
| Constant | `UPPER_SNAKE_CASE` | `MAX_RETRY` |
| Private | `_` prefix | `_internal` |
| Type variable | `PascalCase` | `T`, `UserT` |