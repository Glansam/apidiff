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
    [Fact]
    public void Compare_WhenRequiredRequestFieldAdded_ShouldReturnBreakingEvent()
    {
        // Arrange
        var oldJson = @"
{
  ""openapi"": ""3.0.0"",
  ""info"": { ""title"": ""Test API"", ""version"": ""1.0"" },
  ""paths"": {
    ""/users"": {
      ""post"": {
        ""requestBody"": {
          ""content"": {
            ""application/json"": {
              ""schema"": {
                ""type"": ""object"",
                ""properties"": {
                  ""name"": { ""type"": ""string"" },
                  ""email"": { ""type"": ""string"" }
                },
                ""required"": [ ""name"" ]
              }
            }
          }
        },
        ""responses"": { ""201"": { ""description"": ""Created"" } }
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
      ""post"": {
        ""requestBody"": {
          ""content"": {
            ""application/json"": {
              ""schema"": {
                ""type"": ""object"",
                ""properties"": {
                  ""name"": { ""type"": ""string"" },
                  ""email"": { ""type"": ""string"" }
                },
                ""required"": [ ""name"", ""email"" ]
              }
            }
          }
        },
        ""responses"": { ""201"": { ""description"": ""Created"" } }
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
        Assert.Equal("BREAKING: required field 'email' added to request body for POST /users (application/json)", breakingEvent.Message);
    }
}
