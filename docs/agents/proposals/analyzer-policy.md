# Analyzer policy

`.editorconfig` now lists the unused-code rules (IDE0005, IDE0051, IDE0052) as suggestions. Making them build warnings needs `GenerateDocumentationFile` (IDE0005 only runs in the compiler with it), which in turn asks for XML docs on all public types (CS1591) or a suppression.

Proposal
- Turn on `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` and `<AnalysisLevel>latest-recommended</AnalysisLevel>` in `Directory.Build.props`, and set `TreatWarningsAsErrors` only in CI.
- Fix or suppress what shows up in one dedicated pass (expected: a few dozen style warnings).

Not done here because it changes what a plain `dotnet build` reports for every contributor.
