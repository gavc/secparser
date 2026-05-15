# Contributing

## Development

Requirements:

- Windows 10 build 19041 or later, or Windows 11
- .NET 10 SDK

Common commands:

```powershell
dotnet restore SecParser.slnx
dotnet build SecParser.slnx
dotnet test SecParser.slnx
dotnet run --project SecParser.UI
```

## Pull Requests

Before opening a pull request:

- run `dotnet test SecParser.slnx`;
- avoid committing real `.evtx` files or generated `bin/` and `obj/` output;
- document parser behavior changes with focused unit tests;
- keep exported reports, collection manifests, and screenshots out of source control unless they are sanitized fixtures.

## Sample Data

Windows Security logs often contain sensitive identities, network addresses, command lines, and host information. Use synthetic or sanitized data for tests and examples.
