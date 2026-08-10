# Contributing

Thanks for your interest in improving the AI copilot tools for Advanced Permissions. Issues, ideas and
pull requests are all welcome.

## Before you start

This package is an optional companion to
[Advanced Permissions for Umbraco](https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions).
It adds Umbraco AI copilot tools on top of that package's permission-resolution engine. It does not own
the permission model itself. If your idea is about how permissions resolve, it belongs in the base
package's repository. If it is about how the copilot explains them, it belongs here.

For anything larger than a bug fix, please
[open an issue](https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions.AI/issues) first, so we
can agree on the direction before you spend time on it.

## Getting set up

You need the .NET 10 SDK. There is no frontend build; the `App_Plugins` assets are hand-written static
files.

```bash
git clone https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions.AI.git
cd Umbraco.Community.AdvancedPermissions.AI
dotnet build
dotnet test
```

To exercise the tools end to end you need a running Umbraco with Umbraco AI and your own LLM provider
key. See "Verifying the copilot locally" in the [README](README.md). The test site in
`tests/Umbraco.Community.AdvancedPermissions.AI.TestSite` is already wired up for it.

## House rules

Tests come first. Every behavioural change, whether a feature, a bug fix or a refactor, starts with a
failing unit test. The suite is xUnit with NSubstitute and lives in
`tests/Umbraco.Community.AdvancedPermissions.AI.Tests`.

The package is read-only. The tools read and explain permissions; they never write them. The fix
suggestions describe changes an administrator could make, by simulating them against the pure resolver.
Nothing is applied. Please keep it that way. Permission writes are tracked separately in
[issue #33](https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions/issues/33) on the base
repository.

The copilot must never guess. Anything the model states as fact has to come from a tool result or from
the grounding, not from its own knowledge. A suggested fix has to be confirmed by re-resolution before it
is offered.

Terminology is enforced, and it is editor-facing. A stored record is an entry, so write "a Deny entry",
or use the verb: "deleting is denied". Never use Allow or Deny as a bare noun. An action is a permission,
so write "the Delete permission" rather than bare "Delete". The collections are user groups, never roles.
"Node" stays the generic word for a piece of content. Tests enforce this against the tool descriptions,
the grounding string, and the tool schema, including argument names and returned field names. The one
exception is code that carries a value straight from the base package, whose API is role-named
throughout; those keep the original name so it stays obvious where the value came from.

Definitions live in the concepts tool, not in the system prompt. The always-on grounding carries only
style, the read-only stance, and a pointer to that tool, because its token cost is paid on every agent
run. When you add a definition, add it to the tool. A test fails if it drifts back into the prompt.

Code style is a build requirement. The `.editorconfig` rules are enforced during `dotnet build`, with
warnings treated as errors, so file-scoped namespaces, `var` and primary constructors are not optional.
Every type, method and property needs an XML doc comment, whatever its accessibility.

## Pull requests

Branch off `main`. It is protected and takes squash merges through a pull request only. Keep the pull
request focused, and make sure `dotnet build` and `dotnet test` are both clean. Pull request titles
become the auto-generated release notes, so write them as a reader-facing summary of the change.

## Reporting bugs

Please include your Umbraco version, the versions of `Umbraco.AI` and
`Umbraco.Community.AdvancedPermissions`, which LLM provider and model you are using, and, if the copilot
answered wrongly, the prompt you typed and the answer you got. The tool output matters more than the
phrasing, so say which tool fired if you can tell.

## Licence

By contributing you agree that your contributions are licensed under the [MIT Licence](LICENSE).
