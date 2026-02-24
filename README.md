# ApiDiff

**Stop breaking your downstream clients.**

ApiDiff is a lightning-fast CLI tool that compares two OpenAPI/Swagger specifications and immediately highlights the breaking changes. Perfect for local development or integrating into your CI pipeline to catch API contract violations before they reach production.

### Features
- Deterministic detection of breaking changes
- Outputs specific endpoints and reasons for the failure
- Lightweight and fast

### Example Output
```bash
> apidiff compare --old old.json --new new.json
BREAKING: DELETE /users/{id} removed
```

### Catch Breaking Changes in CI
The Pro version of ApiDiff includes the `--fail-on-breaking` flag, which automatically exits with a non-zero code to block PRs that break API contracts.

```bash
# Block CI if breaking changes are introduced:
> apidiff compare --old prev.json --new curr.json --fail-on-breaking
```

### Installation (Global Tool)
```bash
dotnet tool install --global apidiff
```

### Local Development Usage
If you haven't installed it globally, you can run it directly from source:
```bash
dotnet run --project src/ApiDiff.Cli/ApiDiff.Cli.csproj compare --old prev.json --new curr.json
```

### Exit Codes
- `0`: No breaking changes (or `fail-on-breaking` not specified)
- `2`: Breaking changes detected (with `--fail-on-breaking` flag)
- `64`: Invalid arguments or file processing error

### Supported Specs
- Supports OpenAPI 3.x (JSON)

---

**🔥 Upgrade to ApiDiff Pro!**
Get URL/directory scanning, Markdown/HTML reports, CI failure mode, and AI-powered Pull Request summaries.
👉 [Buy ApiDiff Pro here](https://gumroad.com/)
