# AI Package Scaffolding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scaffold the `Umbraco.Community.AdvancedPermissions.AI` repo into a buildable, testable skeleton — three projects, all tooling/CPM/pipelines/docs — mirroring the v18 base repo's conventions, pinned to Umbraco v17, with no feature code migrated yet.

**Architecture:** A single Razor-SDK package project (native `[AITool]`s runtime shape, static `App_Plugins` assets, no Core/Data/Client), one xUnit test project, and one minimal bootable Umbraco 17 TestSite. The package depends only on `Umbraco.AI` + the full `Umbraco.Community.AdvancedPermissions` base package; the CMS floor is implicit. Version parity between the NuGet package and the backoffice is guaranteed by a MinVer-driven build target.

**Tech Stack:** .NET 10, Umbraco CMS 17.4.2+, Umbraco.AI 17.0.0, Central Package Management, MinVer, xUnit + NSubstitute, GitHub Actions.

**Reference spec:** [`Memory/Plans/2026-07-08-ai-package-scaffolding-design.md`](2026-07-08-ai-package-scaffolding-design.md)

**Paths:** New repo `D:\github\Umbraco.Community.AdvancedPermissions.AI` (Git Bash: `/d/github/Umbraco.Community.AdvancedPermissions.AI`). v18 base repo `/c/GitHub/UmbracoAdvancedSecurity_v18`. v17 base repo `/c/GitHub/UmbracoAdvancedSecurity` (has branch `v17/feature/umbraco-ai-integration`).

**Commits (maintainer preference — IMPORTANT):** Do **not** run `git commit`/`git push` without the maintainer's explicit go-ahead. The commit step in each task is a **checkpoint**: pause and ask before running it. All work happens directly on `main` (branch protection is added last, in Task 7, after the scaffold is pushed).

**Assumptions to verify at execution:** `Umbraco.Community.AdvancedPermissions` 17.2.0 and `Umbraco.AI` 17.0.0 (+ `Umbraco.AI.Agent.Copilot` 17.0.0) are available on nuget.org. If a restore fails, stop and report — do not invent alternate versions. Confirm exact tooling versions (MinVer, SourceLink, Test.Sdk, coverlet, xunit.runner) against `/c/GitHub/UmbracoAdvancedSecurity_v18/Directory.Packages.props` before writing `Directory.Packages.props`.

---

## File structure

```
D:\github\Umbraco.Community.AdvancedPermissions.AI\
├── .editorconfig                    (copy from v18 base, verbatim)
├── .gitattributes                   (copy from v18 base, verbatim)
├── .gitignore                       (copy from v18 base, verbatim)
├── .github/
│   ├── CODEOWNERS                    (authored)
│   └── workflows/
│       ├── ci.yml                    (authored — v18 minus Node steps)
│       └── publish.yml               (authored — v18 minus Node steps)
├── CLAUDE.md                         (authored)
├── Directory.Build.props             (copy from v18 base, verbatim)
├── Directory.Packages.props          (authored — v17-pinned)
├── LICENSE                           (already present)
├── NuGet.config                      (copy from v18 base, verbatim)
├── README.md                         (from v17 feature branch + edits)
├── RELEASE.md                        (authored)
├── Umbraco.Community.AdvancedPermissions.AI.slnx   (authored)
├── umbraco-marketplace.json          (authored)
├── Memory/Plans/                     (already present — spec + this plan)
├── docs/                             (created empty, .gitkeep)
├── src/Umbraco.Community.AdvancedPermissions.AI/
│   ├── Umbraco.Community.AdvancedPermissions.AI.csproj   (authored)
│   ├── AdvancedPermissionsAiAssembly.cs                   (authored — internal marker, added in Task 3)
│   ├── package_logo_128x128.png                           (copy from v18 base)
│   └── wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions.AI/
│       └── umbraco-package.json                           (authored — placeholder v0.0.0)
└── tests/
    ├── Umbraco.Community.AdvancedPermissions.AI.Tests/
    │   ├── Umbraco.Community.AdvancedPermissions.AI.Tests.csproj   (authored)
    │   └── PackageSmokeTests.cs                                    (authored)
    └── Umbraco.Community.AdvancedPermissions.AI.TestSite/
        ├── Umbraco.Community.AdvancedPermissions.AI.TestSite.csproj (authored)
        ├── Program.cs                                              (copy from v18 base)
        ├── appsettings.json                                        (copy from v18 base)
        ├── appsettings.Development.json                            (copy from v18 base)
        └── Properties/launchSettings.json                          (copy from v18 base)
```

---

## Task 1: Repo foundation (config files + CPM)

**Files:**
- Copy: `.editorconfig`, `.gitattributes`, `.gitignore`, `NuGet.config`, `Directory.Build.props` (verbatim from v18 base)
- Create: `Directory.Packages.props`, `docs/.gitkeep`

- [ ] **Step 1: Copy the verbatim config files from the v18 base repo**

Run:
```bash
SRC=/c/GitHub/UmbracoAdvancedSecurity_v18
DST=/d/github/Umbraco.Community.AdvancedPermissions.AI
cp "$SRC/.editorconfig" "$DST/.editorconfig"
cp "$SRC/.gitattributes" "$DST/.gitattributes"
cp "$SRC/.gitignore" "$DST/.gitignore"
cp "$SRC/NuGet.config" "$DST/NuGet.config"
cp "$SRC/Directory.Build.props" "$DST/Directory.Build.props"
mkdir -p "$DST/docs" && touch "$DST/docs/.gitkeep"
ls -1 "$DST"
```
Expected: the five files plus `docs/` now exist in the destination. (`Directory.Build.props` carries `EnforceCodeStyleInBuild` + `NuGetAuditMode=direct`; the latter is why we need no MailKit/crypto transient-vuln overrides.)

- [ ] **Step 2: Create `Directory.Packages.props`**

Create `D:\github\Umbraco.Community.AdvancedPermissions.AI\Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Base package — the AI companion augments it and resolves its services at runtime.
         Real dependency (flows Abstractions transitively for the compile-time contract). Pinned to
         the v17 line; the -0 upper bound excludes v18 prereleases. -->
    <PackageVersion Include="Umbraco.Community.AdvancedPermissions" Version="[17.2.0,18.0.0-0)" />

    <!-- Umbraco AI — the [AITool] runtime. Realigned to the CMS major in the 2026.06 releases, so
         17.0.0 targets Umbraco.Cms 17.x and requires Umbraco.Cms >= 17.4.2. Agent/providers/Copilot
         are TestSite-only (manual verification); providers are referenced by the human when testing. -->
    <PackageVersion Include="Umbraco.AI" Version="17.0.0" />
    <PackageVersion Include="Umbraco.AI.Agent" Version="17.0.0" />
    <PackageVersion Include="Umbraco.AI.Agent.Copilot" Version="17.0.0" />
    <PackageVersion Include="Umbraco.AI.Anthropic" Version="17.0.0" />
    <PackageVersion Include="Umbraco.AI.OpenAI" Version="17.0.0" />

    <!-- Umbraco CMS meta — TestSite only. The ONE place the 17.4.2 floor is explicit (CPM requires
         a version for the direct reference); it matches Umbraco.AI's requirement. The package
         project declares NO direct Umbraco.Cms.* references — they flow transitively. -->
    <PackageVersion Include="Umbraco.Cms" Version="[17.4.2,18.0.0)" />

    <!-- TestSite infra -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.6" />

    <!-- Build / versioning — tooling tracks the v18 base repo, NOT the v17 Umbraco line. -->
    <PackageVersion Include="MinVer" Version="7.0.0" />
    <PackageVersion Include="Microsoft.SourceLink.GitHub" Version="10.0.300" />

    <!-- Testing — tooling tracks the v18 base repo. -->
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Commit (checkpoint — ask first)**

```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
git add .editorconfig .gitattributes .gitignore NuGet.config Directory.Build.props Directory.Packages.props docs/.gitkeep
git commit -m "chore: add repo foundation config and central package management"
```

---

## Task 2: Package project (buildable + packable)

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.AI/Umbraco.Community.AdvancedPermissions.AI.csproj`
- Create: `src/Umbraco.Community.AdvancedPermissions.AI/wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions.AI/umbraco-package.json`
- Copy: `src/Umbraco.Community.AdvancedPermissions.AI/package_logo_128x128.png`
- Create: `README.md` (repo root — packed into the NuGet)

- [ ] **Step 1: Create the package `.csproj`**

Create `src/Umbraco.Community.AdvancedPermissions.AI/Umbraco.Community.AdvancedPermissions.AI.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RootNamespace>Umbraco.Community.AdvancedPermissions.AI</RootNamespace>
    <AssemblyName>Umbraco.Community.AdvancedPermissions.AI</AssemblyName>
    <!-- Serve wwwroot content at the web root (/) so App_Plugins files are reachable at
         /App_Plugins/... as Umbraco expects. Mirrors the base package. -->
    <StaticWebAssetBasePath>/</StaticWebAssetBasePath>

    <!-- NuGet package metadata -->
    <IsPackable>true</IsPackable>
    <PackageId>Umbraco.Community.AdvancedPermissions.AI</PackageId>
    <Title>Advanced Permissions for Umbraco — AI Copilot Tools</Title>
    <Authors>Luuk Peters</Authors>
    <Copyright>Copyright (c) Luuk Peters</Copyright>
    <Description>Optional AI companion for Umbraco.Community.AdvancedPermissions. Exposes Umbraco AI copilot tools that explain effective permissions, find which user groups can access a node, and audit permission configuration.</Description>
    <PackageTags>umbraco;permissions;cms;ai;copilot;umbraco-marketplace</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions.AI</RepositoryUrl>
    <PackageProjectUrl>https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions.AI</PackageProjectUrl>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageIcon>package_logo_128x128.png</PackageIcon>

    <!-- Versioning: MinVer derives version from git tags (e.g. v17.0.0). Standalone repo, so a
         plain "v" prefix (no "ai-v" disambiguation needed). -->
    <MinVerTagPrefix>v</MinVerTagPrefix>
    <MinVerAutoIncrement>minor</MinVerAutoIncrement>
    <MinVerMinimumMajorMinor>17.0</MinVerMinimumMajorMinor>

    <!-- Symbols package + SourceLink -->
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <DeterministicSourcePaths Condition="'$(GITHUB_ACTIONS)' == 'true'">true</DeterministicSourcePaths>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
    <None Include="package_logo_128x128.png" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="MinVer" PrivateAssets="all" />
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="all" />
    <!-- Real dependencies (flow to the NuGet package). Umbraco.Cms.* arrive transitively via both. -->
    <PackageReference Include="Umbraco.AI" />
    <PackageReference Include="Umbraco.Community.AdvancedPermissions" />
  </ItemGroup>

  <!-- Expose internal types (e.g. the assembly marker) to the test project so they can be tested
       without widening accessibility in the shipped package. -->
  <ItemGroup>
    <InternalsVisibleTo Include="Umbraco.Community.AdvancedPermissions.AI.Tests" />
  </ItemGroup>

  <!-- Sync umbraco-package.json version with the MinVer-derived version at build time. The source
       file keeps "0.0.0" as a placeholder; this target replaces it. Guarantees the backoffice
       version and the NuGet version share one MinVer source. Mirrors the base package. -->
  <UsingTask TaskName="SetJsonVersion"
             TaskFactory="RoslynCodeTaskFactory"
             AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
    <ParameterGroup>
      <FilePath ParameterType="System.String" Required="true" />
      <VersionValue ParameterType="System.String" Required="true" />
    </ParameterGroup>
    <Task>
      <Code Type="Fragment" Language="cs"><![CDATA[
var text = System.IO.File.ReadAllText(FilePath);
text = System.Text.RegularExpressions.Regex.Replace(
    text, @"""version"":\s*""[^""]*""", $@"""version"": ""{VersionValue}""");
System.IO.File.WriteAllText(FilePath, text);
Log.LogMessage(MessageImportance.High, $"Set version in {FilePath} to {VersionValue}");
      ]]></Code>
    </Task>
  </UsingTask>

  <Target Name="SyncUmbracoPackageVersion" AfterTargets="MinVer"
          Condition="'$(MinVerVersion)' != ''">
    <SetJsonVersion
      FilePath="$(ProjectDir)wwwroot\App_Plugins\Umbraco.Community.AdvancedPermissions.AI\umbraco-package.json"
      VersionValue="$(MinVerVersion)" />
  </Target>

</Project>
```

- [ ] **Step 2: Create the placeholder `umbraco-package.json`**

Create `src/Umbraco.Community.AdvancedPermissions.AI/wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions.AI/umbraco-package.json`:
```json
{
  "name": "Advanced Permissions for Umbraco — AI Copilot Tools",
  "version": "0.0.0",
  "allowPublicAccess": false,
  "extensions": []
}
```
(No `$schema` — there is no node_modules in this repo. Localization extensions arrive with the code migration. The `0.0.0` placeholder is overwritten on each build by `SyncUmbracoPackageVersion`. The file **must** exist or that target throws.)

- [ ] **Step 3: Copy the package icon and create the root README**

Run:
```bash
SRC=/c/GitHub/UmbracoAdvancedSecurity_v18
V17=/c/GitHub/UmbracoAdvancedSecurity
DST=/d/github/Umbraco.Community.AdvancedPermissions.AI
cp "$SRC/src/Umbraco.Community.AdvancedPermissions/package_logo_128x128.png" \
   "$DST/src/Umbraco.Community.AdvancedPermissions.AI/package_logo_128x128.png"
git -C "$V17" show v17/feature/umbraco-ai-integration:src/Umbraco.Community.AdvancedPermissions.AI/README.md > "$DST/README.md"
ls -l "$DST/README.md" "$DST/src/Umbraco.Community.AdvancedPermissions.AI/package_logo_128x128.png"
```
Expected: both files exist (README ~200 lines; PNG ~21 KB).

- [ ] **Step 4: Fix version/path references in the README**

The README came from the mono-repo and describes the intended (post-migration) package; apply these exact edits so versions and paths are correct for this repo. Using the Edit tool on `README.md`:

Edit A (Umbraco floor):
- old: `- **Umbraco CMS 17.4.0+** (.NET 10) — required by Umbraco AI.`
- new: `- **Umbraco CMS 17.4.2+** (.NET 10) — required by Umbraco AI.`

Edit B (Umbraco AI version, requirements section):
- old: `**[Umbraco AI](https://github.com/umbraco/Umbraco.AI) 1.14.0**, installed and configured with an LLM`
- new: `**[Umbraco AI](https://github.com/umbraco/Umbraco.AI) 17.0.0**, installed and configured with an LLM`

Edit C (remaining `1.14.0` in the "Read-only first" design note, `replace_all` on the exact string):
- old: `1.14.0`
- new: `17.0.0`
- (There is one remaining occurrence: "at Umbraco AI 17.0.0 a backend tool's `IsDestructive` flag…". This behavioural claim was observed at the old 1.14.0; flag it to **re-verify against 17.0.0 during the code-migration session**.)

Edit D (TestSite project name, `replace_all` on the exact string):
- old: `Umbraco.Community.AdvancedPermissions.TestSite`
- new: `Umbraco.Community.AdvancedPermissions.AI.TestSite`

(Leave the GitHub issue links (#32/#33) pointing at the base repo for now — retargeting them to this repo's issue tracker is a follow-up the maintainer can decide.)

- [ ] **Step 5: Restore, build, and pack the package project**

Run:
```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
dotnet build src/Umbraco.Community.AdvancedPermissions.AI/ --configuration Release
dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/ --configuration Release --output ./artifacts
```
Expected: build succeeds (a Razor library with zero `.cs` files builds cleanly). Pack produces `artifacts/Umbraco.Community.AdvancedPermissions.AI.<version>.nupkg` + `.snupkg`. If restore fails on `Umbraco.AI` or `Umbraco.Community.AdvancedPermissions`, STOP and report (see Assumptions).

- [ ] **Step 6: Verify the version-sync target and package contents**

Run:
```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
# The build stamped the MinVer version into umbraco-package.json:
grep '"version"' src/Umbraco.Community.AdvancedPermissions.AI/wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions.AI/umbraco-package.json
# Inspect the nupkg dependencies + packed files:
unzip -l artifacts/*.nupkg | grep -Ei 'readme|logo|umbraco-package|nuspec'
unzip -p artifacts/*.nupkg '*.nuspec' | grep -Ei 'dependency id|Umbraco'
```
Expected: the `"version"` is no longer `0.0.0` (it matches the MinVer output, e.g. `17.0.0-alpha.0.x` with no tags yet); the nupkg contains `README.md`, `package_logo_128x128.png`, `staticwebassets/.../umbraco-package.json`; the `.nuspec` declares dependencies on `Umbraco.AI` and `Umbraco.Community.AdvancedPermissions` and **no** `Umbraco.Cms.*` direct entries. (Step 7 resets the manifest back to the `0.0.0` placeholder before committing.)

- [ ] **Step 7: Reset the stamped placeholder, then commit (checkpoint — ask first)**

Ensure the committed manifest holds the `0.0.0` placeholder (undo the build-time stamp):
```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
# Re-write version to placeholder if the build stamped it:
sed -i 's/"version": *"[^"]*"/"version": "0.0.0"/' \
  src/Umbraco.Community.AdvancedPermissions.AI/wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions.AI/umbraco-package.json
git add src/Umbraco.Community.AdvancedPermissions.AI/Umbraco.Community.AdvancedPermissions.AI.csproj \
        src/Umbraco.Community.AdvancedPermissions.AI/package_logo_128x128.png \
        src/Umbraco.Community.AdvancedPermissions.AI/wwwroot \
        README.md
git commit -m "feat: add AI companion package project with packaging + version sync"
```

---

## Task 3: Unit test project + smoke test (TDD)

**Files:**
- Create: `tests/Umbraco.Community.AdvancedPermissions.AI.Tests/Umbraco.Community.AdvancedPermissions.AI.Tests.csproj`
- Create: `tests/Umbraco.Community.AdvancedPermissions.AI.Tests/PackageSmokeTests.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions.AI/AdvancedPermissionsAiAssembly.cs` (the marker the test needs — created in the "make it pass" step)

- [ ] **Step 1: Create the test `.csproj`**

Create `tests/Umbraco.Community.AdvancedPermissions.AI.Tests/Umbraco.Community.AdvancedPermissions.AI.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <RootNamespace>Umbraco.Community.AdvancedPermissions.AI.Tests</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Umbraco.Community.AdvancedPermissions.AI\Umbraco.Community.AdvancedPermissions.AI.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Write the failing smoke test**

Create `tests/Umbraco.Community.AdvancedPermissions.AI.Tests/PackageSmokeTests.cs`:
```csharp
using Umbraco.Community.AdvancedPermissions.AI;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Smoke tests that verify the package assembly is wired up correctly — it builds, is referenced by
/// the test project, exposes its internals to tests, and loads with the expected identity.
/// </summary>
public class PackageSmokeTests
{
    /// <summary>
    /// The internal assembly marker is visible to the test project (via <c>InternalsVisibleTo</c>)
    /// and its assembly carries the expected simple name.
    /// </summary>
    [Fact]
    public void Package_assembly_is_referenced_and_internals_visible()
    {
        var assemblyName = typeof(AdvancedPermissionsAiAssembly).Assembly.GetName().Name;

        Assert.Equal("Umbraco.Community.AdvancedPermissions.AI", assemblyName);
    }
}
```

- [ ] **Step 3: Run the test to verify it FAILS to compile**

Run:
```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
dotnet test tests/Umbraco.Community.AdvancedPermissions.AI.Tests/ --configuration Release
```
Expected: FAIL — compile error `CS0246`/`CS0103`: the type `AdvancedPermissionsAiAssembly` does not exist yet.

- [ ] **Step 4: Add the marker type to the package to make it pass**

Create `src/Umbraco.Community.AdvancedPermissions.AI/AdvancedPermissionsAiAssembly.cs`:
```csharp
namespace Umbraco.Community.AdvancedPermissions.AI;

/// <summary>
/// Internal marker type identifying the Advanced Permissions AI package assembly. It is a stable
/// anchor for assembly scanning and tests until the package's feature types are added; it carries
/// no behaviour of its own.
/// </summary>
internal static class AdvancedPermissionsAiAssembly;
```

- [ ] **Step 5: Run the test to verify it PASSES**

Run:
```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
dotnet test tests/Umbraco.Community.AdvancedPermissions.AI.Tests/ --configuration Release
```
Expected: PASS — 1 test passed.

- [ ] **Step 6: Commit (checkpoint — ask first)**

```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
git add tests/Umbraco.Community.AdvancedPermissions.AI.Tests \
        src/Umbraco.Community.AdvancedPermissions.AI/AdvancedPermissionsAiAssembly.cs
git commit -m "test: add unit test project with package smoke test"
```

---

## Task 4: Minimal bootable Umbraco 17 TestSite

**Files:**
- Create: `tests/Umbraco.Community.AdvancedPermissions.AI.TestSite/Umbraco.Community.AdvancedPermissions.AI.TestSite.csproj`
- Copy: `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json` (from v18 base TestSite)

- [ ] **Step 1: Create the TestSite `.csproj`**

Create `tests/Umbraco.Community.AdvancedPermissions.AI.TestSite/Umbraco.Community.AdvancedPermissions.AI.TestSite.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <RootNamespace>Umbraco.Community.AdvancedPermissions.AI.TestSite</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Umbraco.Community.AdvancedPermissions.AI\Umbraco.Community.AdvancedPermissions.AI.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Umbraco.Cms" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>

  <!--
    Umbraco AI runtime + copilot chat UI, for manual verification of the AI companion package.
    Umbraco.AI is the runtime meta-package (also referenced by the AI package); Umbraco.AI.Agent.Copilot
    adds the backoffice copilot chat UI and transitively pulls in Umbraco.AI.Agent + Umbraco.AI.Core.
    A human must still add a provider package (Umbraco.AI.OpenAI / .Anthropic — versions are already in
    Directory.Packages.props) and a key before the copilot can run. See README "Local verification".
  -->
  <ItemGroup>
    <PackageReference Include="Umbraco.AI" />
    <PackageReference Include="Umbraco.AI.Agent.Copilot" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Copy the boot files from the v18 base TestSite**

Run:
```bash
SRC=/c/GitHub/UmbracoAdvancedSecurity_v18/tests/Umbraco.Community.AdvancedPermissions.TestSite
DST=/d/github/Umbraco.Community.AdvancedPermissions.AI/tests/Umbraco.Community.AdvancedPermissions.AI.TestSite
mkdir -p "$DST/Properties"
cp "$SRC/Program.cs" "$DST/Program.cs"
cp "$SRC/appsettings.json" "$DST/appsettings.json"
cp "$SRC/appsettings.Development.json" "$DST/appsettings.Development.json"
cp "$SRC/Properties/launchSettings.json" "$DST/Properties/launchSettings.json"
ls -1 "$DST" "$DST/Properties"
```
Expected: the four files exist. (These are generic Umbraco boot files: unattended install auto-creates an admin + SQLite DB on first run. No Views/media are needed to build or boot.)

- [ ] **Step 3: Build the TestSite**

Run:
```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
dotnet build tests/Umbraco.Community.AdvancedPermissions.AI.TestSite/ --configuration Release
```
Expected: build succeeds. Transitive-dependency vulnerability warnings do NOT fail the build (`NuGetAuditMode=direct` from `Directory.Build.props`). If restore fails on an `Umbraco.AI.*` package, STOP and report.

- [ ] **Step 4: Commit (checkpoint — ask first)**

```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
git add tests/Umbraco.Community.AdvancedPermissions.AI.TestSite
git commit -m "test: add minimal bootable Umbraco 17 test site wired to the AI package + copilot"
```

---

## Task 5: Solution file, repo metadata & docs + full green gate

**Files:**
- Create: `Umbraco.Community.AdvancedPermissions.AI.slnx`
- Create: `CLAUDE.md`, `RELEASE.md`, `umbraco-marketplace.json`, `.github/CODEOWNERS`

- [ ] **Step 1: Create the solution file (`.slnx`)**

Create `Umbraco.Community.AdvancedPermissions.AI.slnx`:
```xml
<Solution>
  <Folder Name="/Solution files/">
    <File Path=".editorconfig" />
    <File Path="CLAUDE.md" />
    <File Path="Directory.Build.props" />
    <File Path="Directory.Packages.props" />
    <File Path="NuGet.config" />
    <File Path="README.md" />
    <File Path="RELEASE.md" />
    <File Path="umbraco-marketplace.json" />
  </Folder>
  <Folder Name="/src/">
    <Project Path="src/Umbraco.Community.AdvancedPermissions.AI/Umbraco.Community.AdvancedPermissions.AI.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/Umbraco.Community.AdvancedPermissions.AI.Tests/Umbraco.Community.AdvancedPermissions.AI.Tests.csproj" />
    <Project Path="tests/Umbraco.Community.AdvancedPermissions.AI.TestSite/Umbraco.Community.AdvancedPermissions.AI.TestSite.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 2: Create `.github/CODEOWNERS`**

Create `.github/CODEOWNERS`:
```
# CODEOWNERS for Umbraco.Community.AdvancedPermissions.AI
#
# Each line is a pattern followed by one or more owners.
# The LAST matching pattern takes precedence.
# Docs: https://docs.github.com/en/repositories/managing-your-repositories-settings-and-customizations/customizing-your-repository/about-code-owners

# Default owner for everything in the repo
*       @Luuk1983
```

- [ ] **Step 3: Create `CLAUDE.md`**

Create `CLAUDE.md`:
```markdown
# Umbraco.Community.AdvancedPermissions.AI — Claude Code Guide

## Project Overview

Optional **AI companion** for `Umbraco.Community.AdvancedPermissions`. Ships native C# Umbraco AI
copilot tools (`[AITool]`s via `Umbraco.AI`) that explain effective permissions, find which user
groups can access a node, and audit permission configuration — read-only, grounded in the base
package's permission-resolution engine. **Umbraco v17-first** (targets `net10.0`).

## Guiding principle — where "how" comes from

The **v18 `Umbraco.Community.AdvancedPermissions` repo** (`C:\GitHub\UmbracoAdvancedSecurity_v18`)
is the canonical reference for *how* to do things — csproj/MSBuild structure, workflows,
`Directory.Build.props`, `.editorconfig`, packaging conventions. Only the **targeted versions**
differ: Umbraco-line packages are pinned to v17; build/test tooling tracks the v18 repo. Use the
base repo's `v17/feature/umbraco-ai-integration` branch only for AI-package *content*, not conventions.

## Solution structure

    src/
      Umbraco.Community.AdvancedPermissions.AI/          # The package (Razor SDK): Tools, Services, Models, Scopes, wwwroot/App_Plugins
    tests/
      Umbraco.Community.AdvancedPermissions.AI.Tests/    # xUnit + NSubstitute unit tests
      Umbraco.Community.AdvancedPermissions.AI.TestSite/ # Minimal bootable Umbraco 17 site + AI copilot for manual verification
    Memory/Plans/                                        # Design + implementation plans

No Core/Data/Client projects: the package has no persistence and no Vite/Lit frontend — its
"frontend" is hand-written static `App_Plugins` assets (`umbraco-package.json` + `lang/*.js`).

## Dependencies

- **`Umbraco.Community.AdvancedPermissions`** (`[17.2.0,18.0.0-0)`) — the base package. A real
  runtime dependency: the tools resolve its services from DI. It flows `Abstractions` transitively,
  so the package compiles against the contract without a separate reference. **No direct
  `Umbraco.Cms.*` references** — they arrive transitively (the 17.4.2 floor is implicit via `Umbraco.AI`).
- **`Umbraco.AI`** (`17.0.0`) — the `[AITool]` runtime. Realigned to the CMS major in 2026.06;
  17.0.0 requires `Umbraco.Cms >= 17.4.2`.

## Version sync (backoffice == NuGet)

`SyncUmbracoPackageVersion` (a `SetJsonVersion` RoslynCodeTaskFactory task, `AfterTargets="MinVer"`)
stamps `$(MinVerVersion)` into `wwwroot/App_Plugins/.../umbraco-package.json`. The NuGet version is
the same MinVer value, so the two never drift. The committed source keeps a `0.0.0` placeholder.

## Build & run

    dotnet build
    dotnet test
    dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/

    # Manual AI verification (needs your own LLM provider + key — see README):
    dotnet run --project tests/Umbraco.Community.AdvancedPermissions.AI.TestSite --urls http://localhost:5000

## Gotchas

- **Namespace collision with `Umbraco.Cms`**: the project namespace is
  `Umbraco.Community.AdvancedPermissions.AI`, so the compiler sees `Umbraco` as a parent namespace.
  Keep all `Umbraco.Cms.*` access in `using` directives (or use `global::Umbraco.Cms...`), never
  inline fully-qualified inside a class declared in this namespace.
- **Versioning**: MinVer, `v`-prefixed tags (`v17.x.x`). Cutting a release = pushing a tag;
  `publish.yml` does the rest. Ask before tagging/pushing (maintainer preference; no auto-commit).
- **Reference source**: Umbraco v17 backoffice at `C:\GitHub\UmbracoVersions\v17\src\Umbraco.Web.UI.Client`.
  Do not read `node_modules` — use the reference source.
```

- [ ] **Step 4: Create `RELEASE.md`**

Create `RELEASE.md`:
```markdown
# Release checklist — Advanced Permissions for Umbraco (AI companion)

Repo-specific release notes that complement the generic **`nuget-pre-release`** skill. The skill
owns the phase-by-phase workflow; this file records what only this package knows.

## Release model

- **Personal GitHub package** (owned by Luuk Peters), **not** a Proud Nerds package.
- Published from **GitHub Actions** (`publish.yml`), versioned by **MinVer** from a `v`-prefixed git
  tag (`MinVerTagPrefix=v`, auto-increment minor, minimum `17.0`). Cutting the release = pushing the tag.
- **Cut the release manually in GitHub** by default, for control. Ask before anything tags/pushes.
- **Post-release: N/A.** Do **not** invoke `nuget-post-release` (that is for Proud Nerds packages on
  Azure DevOps). The next version comes from the next tag.
- **Independent cadence** from the base package: this add-on ships its own `v*` tags; its MAJOR
  tracks the shared CMS major (17.x).

## Dependency hygiene

Central versions live in `Directory.Packages.props`:

- `Umbraco.Community.AdvancedPermissions` is pinned `[17.2.0,18.0.0-0)` — keep the floor at a
  released stable base version and keep the `-0` upper bound (excludes v18 prereleases).
- `Umbraco.AI` (and the `Umbraco.AI.*` TestSite packages) pinned to the 17.x line.
- The package declares **no** direct `Umbraco.Cms.*` — its floor is implicit via `Umbraco.AI`. The
  only explicit `Umbraco.Cms` pin (TestSite) must stay `>= 17.4.2` (Umbraco.AI's requirement).

## Release notes

`publish.yml` uses `softprops/action-gh-release@v2` with `generate_release_notes: true` — the body
is auto-generated from merged PRs since the previous tag. No `CHANGELOG.md`, no `<PackageReleaseNotes>`.
For a first stable release, curate the body by hand.

## First-publish prerequisites (one-time repo settings)

`publish.yml` uses NuGet **trusted publishing** (`NuGet/login@v1`) and a `production` environment:

- Configure a trusted-publishing policy for `Umbraco.Community.AdvancedPermissions.AI` on nuget.org
  bound to this repo's `publish.yml`.
- Create a `production` GitHub environment and the `NUGET_USER` secret.

## Documentation to keep in sync

- `README.md` — the copilot tools and example prompts; keep in step with the actual `[AITool]`s.
  Prerequisites must state the correct Umbraco floor (**17.4.2+**) and `Umbraco.AI` version.
- Backoffice localization ships as `wwwroot/App_Plugins/.../lang/*.js` (en + nl) once migrated — keep in sync.
- `umbraco-marketplace.json` — review Description / tags / URLs each release; nothing compiles it.

## Build / test / pack

    dotnet build
    dotnet test
    dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/

When inspecting the `.nupkg`: README + `package_logo_128x128.png` packed; `Umbraco.AI` +
`Umbraco.Community.AdvancedPermissions` declared as dependencies (no bundled DLLs); and
`wwwroot/.../umbraco-package.json` version synced by the MinVer build target.
```

- [ ] **Step 5: Create `umbraco-marketplace.json`**

Create `umbraco-marketplace.json`:
```json
{
  "$schema": "https://marketplace.umbraco.com/umbraco-marketplace-schema.json",
  "Title": "Advanced Permissions for Umbraco — AI Copilot Tools",
  "Category": "Developer Tools",
  "AlternateCategory": "Editor Tools",
  "Description": "Optional AI companion for Advanced Permissions for Umbraco. Makes the Umbraco backoffice copilot permission-aware: ask in plain language who can do what, why an action is denied, and audit permission configuration — every answer grounded in the package's permission-resolution engine, read-only.",
  "PackageType": "Package",
  "LicenseTypes": ["Free"],
  "AuthorDetails": {
    "Name": "Luuk Peters",
    "Description": "Umbraco developer building packages that extend the CMS with enterprise-grade features. Creator of Advanced Permissions for Umbraco.",
    "Url": "https://github.com/Luuk1983",
    "SyncContributorsFromRepository": true
  },
  "DocumentationUrl": "https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions.AI",
  "IssueTrackerUrl": "https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions.AI/issues",
  "Tags": [
    "permissions",
    "security",
    "ai",
    "copilot",
    "umbraco ai",
    "access control",
    "audit",
    "user groups",
    "backoffice"
  ],
  "Screenshots": []
}
```

- [ ] **Step 6: Full solution build + test (the green gate)**

Run:
```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
dotnet build --configuration Release
dotnet test --configuration Release --no-build --verbosity normal
```
Expected: the whole solution (all 3 projects) builds; `dotnet test` reports 1 passing test, 0 failed. Reset the stamped placeholder if the build re-stamped it (see Task 2 Step 7 `sed`).

- [ ] **Step 7: Commit (checkpoint — ask first)**

```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
git add Umbraco.Community.AdvancedPermissions.AI.slnx CLAUDE.md RELEASE.md umbraco-marketplace.json .github/CODEOWNERS
# also re-stage the placeholder reset if needed:
git add src/Umbraco.Community.AdvancedPermissions.AI/wwwroot
git commit -m "chore: add solution file, repo metadata and docs"
```

---

## Task 6: GitHub Actions workflows

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/publish.yml`

These mirror the v18 base workflows with the Node.js/`npm` frontend-build steps **removed** (no Vite build) and `dotnet pack` pointed at the AI project.

- [ ] **Step 1: Create `.github/workflows/ci.yml`**

Create `.github/workflows/ci.yml`:
```yaml
name: CI

on:
  pull_request:
    branches: [main, '*/main']

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0 # Full history needed for MinVer

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Test
        run: dotnet test --configuration Release --no-build --verbosity normal

      - name: Pack (verify package builds)
        run: dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/ --configuration Release --output ./artifacts

      - name: Upload package artifact
        uses: actions/upload-artifact@v4
        with:
          name: nuget-package
          path: ./artifacts/*.nupkg
          retention-days: 7
```

- [ ] **Step 2: Create `.github/workflows/publish.yml`**

Create `.github/workflows/publish.yml`:
```yaml
name: Publish to NuGet

on:
  push:
    tags: ['v*.*.*']

permissions:
  id-token: write # Required for NuGet trusted publishing
  contents: write # Required for creating GitHub releases

jobs:
  publish:
    runs-on: ubuntu-latest
    environment: production

    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0 # Full history needed for MinVer

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --configuration Release --no-restore

      - name: Test
        run: dotnet test --configuration Release --no-build --verbosity normal

      - name: Pack
        run: dotnet pack src/Umbraco.Community.AdvancedPermissions.AI/ --configuration Release --output ./artifacts

      - name: Login to NuGet (trusted publishing)
        id: nuget-login
        uses: NuGet/login@v1
        with:
          user: ${{ secrets.NUGET_USER }}

      - name: Push to NuGet
        run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ steps.nuget-login.outputs.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: ./artifacts/*.nupkg
          generate_release_notes: true
          prerelease: ${{ contains(github.ref_name, '-') }}
```

- [ ] **Step 3: Sanity-check the YAML**

Run:
```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
# Confirm no Node/npm steps leaked in and the pack path is the AI project:
grep -n "npm\|node-version\|setup-node" .github/workflows/*.yml || echo "OK: no Node steps"
grep -n "dotnet pack" .github/workflows/*.yml
```
Expected: "OK: no Node steps"; both `dotnet pack` lines target `src/Umbraco.Community.AdvancedPermissions.AI/`.

- [ ] **Step 4: Commit (checkpoint — ask first)**

```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
git add .github/workflows/ci.yml .github/workflows/publish.yml
git commit -m "ci: add build/test CI and tag-driven NuGet publish workflows"
```

---

## Task 7: Push, then enable branch protection (outward-facing — explicit approval required)

This changes the GitHub repo's settings and requires the scaffold to be on `main` first. Do **only** with the maintainer's explicit go-ahead. Requires `gh` authenticated with admin on the repo.

- [ ] **Step 1: Push `main` (checkpoint — ask first)**

```bash
cd /d/github/Umbraco.Community.AdvancedPermissions.AI
git push origin main
```

- [ ] **Step 2: Read the base repo's protection to mirror it**

Run:
```bash
gh api repos/Luuk1983/Umbraco.Community.AdvancedPermissions/branches/main/protection
```
Expected: prints the base repo's protection (required PR reviews, required status checks, enforce_admins, etc.). Use this as the source of truth for the settings to replicate; note the required status-check contexts.

- [ ] **Step 3: Apply equivalent protection to the AI repo**

The required status-check context must match this repo's CI job name (`build-and-test`). Adjust the payload below to match whatever Step 2 reported for the base repo (review count, `enforce_admins`, `require_last_push_approval`, etc.):
```bash
gh api --method PUT repos/Luuk1983/Umbraco.Community.AdvancedPermissions.AI/branches/main/protection \
  --input - <<'JSON'
{
  "required_status_checks": {
    "strict": true,
    "contexts": ["build-and-test"]
  },
  "enforce_admins": false,
  "required_pull_request_reviews": {
    "required_approving_review_count": 0,
    "require_last_push_approval": false
  },
  "restrictions": null,
  "required_linear_history": false,
  "allow_force_pushes": false,
  "allow_deletions": false
}
JSON
```
Expected: HTTP 200 with the applied protection JSON. (A CI status check named `build-and-test` will only be reported once a PR triggers `ci.yml`; the branch will still require a PR immediately.)

- [ ] **Step 4: Verify**

Run:
```bash
gh api repos/Luuk1983/Umbraco.Community.AdvancedPermissions.AI/branches/main/protection \
  | grep -Ei 'required_status_checks|contexts|allow_force_pushes|allow_deletions|required_pull_request_reviews' -A2
```
Expected: PRs required, force-push/deletion disabled, `build-and-test` a required check — matching the base repo.

---

## Notes for the executor

- **Nothing is migrated from the feature branch** beyond the README and boot-file conventions. Tools/
  services/models/lang files come in a later session (see the spec's "Deferred" section).
- If a `dotnet restore` fails because a pinned Umbraco/Umbraco.AI/base-package version is not on
  nuget.org, STOP and report the exact version — do not substitute.
- Keep the committed `umbraco-package.json` at `0.0.0`; the build stamps it and may dirty the tree.
