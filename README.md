# PersonUI

A Blazor Server front-end for [PersonApi](https://github.com/pixelscribe/PersonApi) — lists, searches, creates, edits, and deletes `Person` records via PersonApi's REST endpoints.

## Requirements

- .NET 10 SDK
- A reachable PersonApi instance

## Configuration

The app needs one setting: PersonApi's base URL, at `PersonApi:BaseUrl`. The app throws on startup if it's missing.

**appsettings.Development.json** (for local dev):
```json
{
  "PersonApi": {
    "BaseUrl": "http://localhost:5094"
  }
}
```

**Environment variable** (`:` becomes `__`, same as PersonApi):
```powershell
$env:PersonApi__BaseUrl = "http://localhost:5094"
```

## Running

```powershell
dotnet run
```

By default (see `Properties/launchSettings.json`):
- HTTP: `http://localhost:5095`
- HTTPS: `https://localhost:7295`

## Testing

```powershell
dotnet test
```

`PersonUI.Tests/` has two kinds of tests, covering different layers:

- **`PersonApiClientTests`** — unit tests for `PersonApiClient` against a mocked `HttpMessageHandler` (`FakeHttpMessageHandler`). No real PersonApi or browser needed; covers request-building and the error-parsing logic in `ExtractErrorAsync`.
- **`PersonCreateTests` / `PersonEditTests` / `HomeTests`** — [bUnit](https://bunit.dev/) component tests that render the actual Razor pages and interact with them (filling forms, clicking buttons, submitting), with `PersonApiClient` wired to the same `FakeHttpMessageHandler`. These cover the pages' own logic — form validation wiring, navigation on success, error display on failure, the delete-confirmation flow via `IJSRuntime` — not just the HTTP client underneath them.

`PersonUITestContext` (shared bUnit base) registers a fake `PersonApiClient` and sets `JSInterop.Mode = JSRuntimeMode.Loose` so unconfigured JS interop calls (e.g. `confirm()`) return safe defaults instead of throwing.

## Docker

```powershell
docker build -t personui .
docker run --rm -p 8080:8080 -e PersonApi__BaseUrl="http://host:5094" personui
```

Listens on port `8080`, no HTTPS inside the container (terminate TLS at a reverse proxy/load balancer in front of it).

## CI/CD

- **`.github/workflows/ci.yml`** — runs on every pull request and push to `master`: builds, then runs the full test suite. Test results are published to the PR via `dorny/test-reporter`.
- **`.github/workflows/release.yml`** — runs after a PR merges to `master`. Computes the next version from [Conventional Commits](https://www.conventionalcommits.org/) messages since the last tag, pushes a git tag, creates a GitHub Release, builds and pushes `pixelscribe/person-ui:<tag>` to Docker Hub, then triggers the [`live-terraform`](https://github.com/pixelscribe/live-terraform) repo's `deploy-webui.yml` workflow with that version — which runs `terraform apply` against `services/webui` to roll the EC2 instance over to the new image.
- `master` is protected by a branch ruleset: changes must go through a PR, and the `test` check from `ci.yml` must pass before merging. Self-approval is allowed (no required review count).
