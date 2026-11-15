using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace AsiBackbone.Core.Tests.RepositoryHygiene;

/// <summary>
/// Regression checks for the informational version that every AsiBackbone package assembly reports.
/// </summary>
/// <remarks>
/// <c>Directory.Build.props</c> once set <c>InformationalVersion</c> to <c>$(Version)+$(RepositoryUrl)</c> before
/// <c>RepositoryUrl</c> was defined, so the URL evaluated empty and every package assembly reported
/// <c>&lt;version&gt;+.&lt;commit&gt;</c> (AsiBackbone/AsiBackbone#938).
/// </remarks>
public sealed class InformationalVersionTests
{
    /// <summary>
    /// Verifies that the shared build properties leave <c>InformationalVersion</c> to the SDK default.
    /// </summary>
    [Fact]
    public void DirectoryBuildPropsLeavesInformationalVersionToSdkDefault()
    {
        var properties = XDocument.Load(Path.Combine(FindRepositoryRoot(), "Directory.Build.props"));

        Assert.Empty(properties.Descendants("InformationalVersion"));
    }

    /// <summary>
    /// Verifies that the Core package assembly reports <c>&lt;version&gt;</c> or <c>&lt;version&gt;+&lt;commit&gt;</c>.
    /// </summary>
    [Fact]
    public void CoreAssemblyReportsWellFormedInformationalVersion()
    {
        Assembly assembly = AssemblyReference.Assembly;
        string? informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        string? fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

        Assert.NotNull(informationalVersion);
        Assert.NotNull(fileVersion);

        // AssemblyVersion stays fixed for the major line, so compare with the release-tracking FileVersion.
        string releaseVersion = Version.Parse(fileVersion).ToString(3);

        Assert.StartsWith(releaseVersion, informationalVersion, StringComparison.Ordinal);
        Assert.False(informationalVersion.EndsWith('+'), $"Informational version '{informationalVersion}' ends with '+'.");
        Assert.DoesNotContain("+.", informationalVersion, StringComparison.Ordinal);
        Assert.DoesNotContain("://", informationalVersion, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AsiBackbone.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root containing AsiBackbone.slnx.");
    }
}
