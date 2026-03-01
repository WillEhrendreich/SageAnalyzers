# Git Hooks

This directory contains version-controlled git hooks.

## Available Hooks

### pre-push
Builds in Release mode (FS3511 safety gate) then auto-increments patch version.

**Features:**
- **FS3511 build gate** — builds Release mode before push to catch "state machine not statically compilable" errors that only surface during code generation
- Increments version in project files
- Creates a git tag with the new version
- Amends the current commit with version changes
- Prevents double-bumping with safeguards

## FS3511 Protection

`Directory.Build.props` promotes FS3511 to an error via `<WarningsAsErrors>`. This means:

- **Every local build** catches FS3511 — no waiting for CI
- **Editor background builds** surface it as a red squiggle (VS, Rider, VS Code w/ Ionide)
- **Pre-push hook** builds Release mode to catch Debug/Release codegen differences

### For consuming projects

Add this to your own `Directory.Build.props`:

```xml
<WarningsAsErrors>$(WarningsAsErrors);FS3511</WarningsAsErrors>
```

FS3511 is emitted during code generation when the F# compiler can't statically compile a `task { }` or `backgroundTask { }` computation expression into a state machine. Common triggers:

- `try/with` blocks containing `let!`/`do!`/`use!` (async resumption points in exception handlers)
- `try/finally` blocks with async operations in the try body
- Complex nesting patterns that exceed the state machine lowering threshold

## Installation

Run the installation script to copy hooks to your `.git/hooks/` directory:

```powershell
pwsh hooks/install-hooks.ps1
```

Or on Unix-like systems:
```bash
./hooks/install-hooks.ps1
```

## Updating Hooks

When hooks are updated in this folder, run the install script again to update your local `.git/hooks/` directory.
