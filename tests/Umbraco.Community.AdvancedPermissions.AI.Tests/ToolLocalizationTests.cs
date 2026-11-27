using System.Reflection;
using System.Text.RegularExpressions;
using Umbraco.AI.Core.Tools;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Guards that every registered <c>[AITool]</c> has a label and description in every shipped backoffice
/// localization file.
/// </summary>
/// <remarks>
/// <para>
/// This closes a genuinely silent failure. Umbraco AI's "Select Tools" dialog localizes each tool through
/// keys derived from its id, and the <c>lang/*.js</c> files are plain static assets: nothing compiles
/// them, nothing validates them, and nothing fails at build or run time when a pair is missing. The only
/// symptom is a raw key such as <c>uaiTool_uapExplainEditorsLabel</c> appearing in the dialog — visible to
/// an administrator, invisible to us.
/// </para>
/// <para>
/// The repo's own release checklist flags "a new [AITool] needs a new pair in <i>both</i> files" as a
/// manual step. A manual step that fails silently is one worth automating, and the v18 update added two
/// tools at once, which is exactly when the step gets missed.
/// </para>
/// <para>
/// Tools and files are both discovered rather than listed, so a new tool or a new locale is covered
/// automatically instead of needing this test updated too.
/// </para>
/// </remarks>
public sealed class ToolLocalizationTests
{
    /// <summary>The directory holding the shipped localization files.</summary>
    private static string LangDirectory =>
        Path.Combine(FindRepoRoot(), "src", "Umbraco.Community.AdvancedPermissions.AI", "wwwroot",
            "App_Plugins", "Umbraco.Community.AdvancedPermissions.AI", "lang");

    /// <summary>
    /// Walks up from the test assembly to the repository root, identified by the solution file.
    /// </summary>
    /// <returns>The repository root path.</returns>
    /// <exception cref="InvalidOperationException">The root could not be located.</exception>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Umbraco.Community.AdvancedPermissions.AI.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    /// <summary>
    /// Converts a tool id to the camelCase form Umbraco AI derives its localization keys from: split on
    /// <c>[-_.\s]+</c>, lowercase the first segment, title-case the rest.
    /// </summary>
    /// <param name="toolId">The tool id, e.g. <c>uap_explain_library_access</c>.</param>
    /// <returns>The camelCase key stem, e.g. <c>uapExplainLibraryAccess</c>.</returns>
    private static string ToCamelCase(string toolId)
    {
        var segments = Regex.Split(toolId, @"[-_.\s]+")
            .Where(seg => seg.Length > 0)
            .ToArray();

        return string.Concat(segments.Select((seg, i) =>
            i == 0
                ? seg.ToLowerInvariant()
                : char.ToUpperInvariant(seg[0]) + seg[1..].ToLowerInvariant()));
    }

    /// <summary>
    /// Every tool id declared by an <see cref="AIToolAttribute"/> in the shipped assembly, paired with
    /// each shipped localization file — so the theory covers tools × locales.
    /// </summary>
    /// <returns>Tool id and localization file path pairs.</returns>
    public static TheoryData<string, string> ToolsAndLocales()
    {
        var toolIds = typeof(Tools.ExplainAccessTool).Assembly
            .GetTypes()
            .Select(t => t.GetCustomAttribute<AIToolAttribute>())
            .Where(a => a is not null)
            .Select(a => a!.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(toolIds);

        var langFiles = Directory.GetFiles(LangDirectory, "*.js").OrderBy(f => f, StringComparer.Ordinal).ToList();
        Assert.NotEmpty(langFiles);

        var data = new TheoryData<string, string>();
        foreach (var toolId in toolIds)
        {
            foreach (var file in langFiles)
            {
                data.Add(toolId, file);
            }
        }

        return data;
    }

    /// <summary>
    /// Each tool has both a label and a description key in each shipped locale. A missing pair shows the
    /// raw key in Umbraco AI's tool picker and fails nothing else.
    /// </summary>
    /// <param name="toolId">The tool id.</param>
    /// <param name="langFile">The localization file to check.</param>
    [Theory]
    [MemberData(nameof(ToolsAndLocales))]
    public void EveryTool_HasALabelAndDescriptionInEveryLocale(string toolId, string langFile)
    {
        var content = File.ReadAllText(langFile);
        var stem = ToCamelCase(toolId);
        var locale = Path.GetFileName(langFile);

        Assert.True(
            content.Contains($"{stem}Label:", StringComparison.Ordinal),
            $"{locale} is missing '{stem}Label' for tool '{toolId}'. Umbraco AI would show the raw key.");
        Assert.True(
            content.Contains($"{stem}Description:", StringComparison.Ordinal),
            $"{locale} is missing '{stem}Description' for tool '{toolId}'. Umbraco AI would show the raw key.");
    }

    /// <summary>
    /// The locales must stay in step with each other: a label present in one file and absent from another
    /// is the same silent defect, just for a subset of users.
    /// </summary>
    [Fact]
    public void EveryLocale_DeclaresTheSameToolKeys()
    {
        var keysByLocale = Directory.GetFiles(LangDirectory, "*.js")
            .ToDictionary(
                f => Path.GetFileName(f)!,
                f => Regex.Matches(File.ReadAllText(f), @"\b(uap[A-Za-z]+)(?:Label|Description):")
                    .Select(m => m.Groups[1].Value)
                    .ToHashSet(StringComparer.Ordinal));

        Assert.True(keysByLocale.Count > 1, "Expected more than one shipped locale.");

        var reference = keysByLocale.First();
        foreach (var (locale, keys) in keysByLocale.Skip(1))
        {
            Assert.True(
                keys.SetEquals(reference.Value),
                $"{locale} and {reference.Key} declare different tool keys. "
                + $"Only in {reference.Key}: {string.Join(", ", reference.Value.Except(keys))}. "
                + $"Only in {locale}: {string.Join(", ", keys.Except(reference.Value))}.");
        }
    }
}
