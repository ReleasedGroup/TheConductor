# Conductor

Conductor is the software delivery control surface for supervising many Symphony instances across projects and repositories.

## Local Development

Run the Blazor host:

```powershell
dotnet run --project src/Conductor.Host/Conductor.Host.csproj
```

Run validation:

```powershell
dotnet restore Conductor.slnx
dotnet build Conductor.slnx --no-restore --warnaserror
dotnet test Conductor.slnx --no-build
```
