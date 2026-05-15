# Third-Party Notices

SecParser uses third-party packages from NuGet. Each package remains subject to
its own license terms.

## QuestPDF

SecParser uses QuestPDF for PDF report generation and sets:

```csharp
QuestPDF.Settings.License = LicenseType.Community;
```

QuestPDF's Community license is intended for eligible users and projects, including
individuals, open-source projects, non-profits, and companies below the annual
gross revenue threshold stated by QuestPDF.

Before using, redistributing, or deploying SecParser in a commercial or
organizational environment, review the current QuestPDF license terms:

- https://www.questpdf.com/license/
- https://github.com/QuestPDF/QuestPDF/blob/main/LICENSE.md

If your use is not eligible for the QuestPDF Community license, you are
responsible for obtaining the appropriate QuestPDF commercial license.

## Other NuGet Packages

SecParser also references packages including:

- ClosedXML
- CommunityToolkit.Mvvm
- System.Diagnostics.EventLog
- xUnit and Microsoft.NET.Test.Sdk for tests

Review package metadata and repository licenses before redistributing modified
builds.
