using System.Reflection;
using EventReservationApp.Mcp.Tools;
using Xunit;

namespace EventReservationApp.Tests.Mcp;

/// <summary>
/// Cross-cutting security checks for the three MCP tools themselves (as
/// opposed to <see cref="SecurityBoundaryTests"/>, which covers the
/// application services underneath them).
///
/// The key guarantee this asserts is structural, not just behavioral: none
/// of the three tool methods declare a parameter through which a caller (or
/// the model generating the tool call) could supply a user id. Because the
/// MCP SDK derives each tool's JSON schema directly from its C# method
/// parameters, this is not just a coding convention - it means the schema
/// offered to the model literally has no field to put a user id in, so
/// there is no "userId injection" payload that could ever reach the tool.
/// </summary>
public class McpToolSecurityTests
{
    private static readonly HashSet<string> ForbiddenParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "userid", "user_id", "currentuserid", "callerid", "onbehalfof", "username", "role", "isadministrator", "admin"
    };

    public static IEnumerable<object[]> ToolMethods()
    {
        yield return new object[] { typeof(SearchEventsTool), nameof(SearchEventsTool.SearchEvents) };
        yield return new object[] { typeof(ManageMyReservationsTool), nameof(ManageMyReservationsTool.ManageMyReservations) };
        yield return new object[] { typeof(GetEventAvailabilityTool), nameof(GetEventAvailabilityTool.GetEventAvailability) };
    }

    [Theory]
    [MemberData(nameof(ToolMethods))]
    public void NoMcpTool_AcceptsAUserIdOrRoleParameter(Type toolType, string methodName)
    {
        var method = toolType.GetMethod(methodName);
        Assert.NotNull(method);

        foreach (var parameter in method!.GetParameters())
        {
            var name = parameter.Name ?? string.Empty;
            Assert.False(
                ForbiddenParameterNames.Contains(name),
                $"{toolType.Name}.{methodName}({name}) would let a caller supply identity/role directly; " +
                "identity must come from the authenticated request only.");
        }
    }

    [Theory]
    [MemberData(nameof(ToolMethods))]
    public void EveryMcpTool_IsMarkedAsAnMcpServerToolType(Type toolType, string methodName)
    {
        // Guards against accidentally expanding the MCP surface: a tool is only
        // callable by an MCP client if the containing type carries this attribute
        // and the method carries [McpServerTool] (checked implicitly by the tool
        // registering successfully in ManageMyReservationsToolTests/etc.).
        _ = methodName;
        var attribute = toolType.GetCustomAttribute<ModelContextProtocol.Server.McpServerToolTypeAttribute>();
        Assert.NotNull(attribute);
    }

    [Fact]
    public void ExactlyThreeMcpToolTypes_AreDefinedInTheAssembly()
    {
        var toolAssembly = typeof(SearchEventsTool).Assembly;
        var toolTypes = toolAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ModelContextProtocol.Server.McpServerToolTypeAttribute>() is not null)
            .ToList();

        Assert.Equal(3, toolTypes.Count);
        Assert.Contains(toolTypes, t => t == typeof(SearchEventsTool));
        Assert.Contains(toolTypes, t => t == typeof(ManageMyReservationsTool));
        Assert.Contains(toolTypes, t => t == typeof(GetEventAvailabilityTool));
    }
}
