# Student Shell Harness

Checks the Unity-independent boundary of the local fallback student shell: local identity validation, truthful training catalog, managed-launch fail-closed routing, and non-persistent local session lifecycle.

Run from the `YardCraneSimulator` directory:

```powershell
dotnet run --project Tools/StudentShellHarness/StudentShellHarness.csproj -c Release
```

This does not load UXML/USS, click UI Toolkit controls, load Unity scenes, authenticate against a server, or prove a Windows Player build.
