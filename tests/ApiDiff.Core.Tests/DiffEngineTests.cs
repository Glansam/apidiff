using System.Linq;
using ApiDiff.Core;
using ApiDiff.Core.Models;
using Xunit;

namespace ApiDiff.Core.Tests;

public class DiffEngineTests
{
    [Fact]
    public void Compare_WhenEndpointIsRemoved_ShouldReturnBreakingEvent()
    {
        // Arrange
        var oldJson = @"
{
  ""openapi"": ""3.0.0"",
  ""info"": { ""title"": ""Test API"", ""version"": ""1.0"" },
  ""paths"": {
    ""/users/{id}"": {
      ""delete"": {
        ""responses"": { ""200"": { ""description"": ""Success"" } }
      }
    },
    ""/users"": {
      ""get"": {
        ""responses"": { ""200"": { ""description"": ""Success"" } }
      }
    }
  }
}
";

        var newJson = @"
{
  ""openapi"": ""3.0.0"",
  ""info"": { ""title"": ""Test API"", ""version"": ""1.0"" },
  ""paths"": {
    ""/users"": {
      ""get"": {
        ""responses"": { ""200"": { ""description"": ""Success"" } }
      }
    }
  }
}
";

        var engine = new DiffEngine();

        // Act
        var results = engine.Compare(oldJson, newJson).ToList();

        // Assert
        Assert.Single(results);
        var breakingEvent = results.First();
        Assert.Equal(DiffSeverity.Breaking, breakingEvent.Severity);
        Assert.Equal("BREAKING: DELETE /users/{id} removed", breakingEvent.Message);
    }
}
