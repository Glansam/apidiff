using System.Linq;
using ApiDiff.Core;
using ApiDiff.Core.Models;
using Xunit;

namespace ApiDiff.Core.Tests;

public class DiffEngineRulesTests
{
    private DiffEngine _engine = new DiffEngine();

    [Fact]
    public void Rule2_RequestRequiredFieldAdded_ShouldReturnBreakingEvent()
    {
        var oldJson = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""post"": { ""requestBody"": { ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" } } } } } } } } } }";
        var newJson = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""post"": { ""requestBody"": { ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"", ""required"": [""name""], ""properties"": { ""name"": { ""type"": ""string"" } } } } } } } } } }";

        var results = _engine.Compare(oldJson, newJson).ToList();

        Assert.Single(results);
        Assert.Equal("BREAKING: required field 'name' added to request body for POST /users (application/json)", results[0].Message);
    }

    [Fact]
    public void Rule3_RequestFieldTypeChanged_ShouldReturnBreakingEvent()
    {
        var oldJson = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""post"": { ""requestBody"": { ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"", ""properties"": { ""age"": { ""type"": ""string"" } } } } } } } } } }";
        var newJson = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""post"": { ""requestBody"": { ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"", ""properties"": { ""age"": { ""type"": ""integer"" } } } } } } } } } }";

        var results = _engine.Compare(oldJson, newJson).ToList();

        Assert.Single(results);
        Assert.Equal("BREAKING: POST /users request field 'age' changed type from string to integer", results[0].Message);
    }

    [Fact]
    public void Rule4_ResponseFieldRemoved_ShouldReturnBreakingEvent()
    {
        var oldJson = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""get"": { ""responses"": { ""200"": { ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"", ""properties"": { ""id"": { ""type"": ""string"" }, ""name"": { ""type"": ""string"" } } } } } } } } } } }";
        var newJson = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""get"": { ""responses"": { ""200"": { ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"", ""properties"": { ""id"": { ""type"": ""string"" } } } } } } } } } } }";

        var results = _engine.Compare(oldJson, newJson).ToList();

        Assert.Single(results);
        Assert.Equal("BREAKING: GET /users response removed field 'name'", results[0].Message);
    }

    [Fact]
    public void Rule4_ResponseFieldTypeChanged_ShouldReturnBreakingEvent()
    {
        var oldJson = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""get"": { ""responses"": { ""200"": { ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"", ""properties"": { ""id"": { ""type"": ""integer"" } } } } } } } } } } }";
        var newJson = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""get"": { ""responses"": { ""200"": { ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"", ""properties"": { ""id"": { ""type"": ""string"" } } } } } } } } } } }";

        var results = _engine.Compare(oldJson, newJson).ToList();

        Assert.Single(results);
        Assert.Equal("BREAKING: GET /users response field 'id' changed type from integer to string", results[0].Message);
    }

    [Fact]
    public void Rule5_EnumValueRemoved_ShouldReturnBreakingEvent()
    {
        var oldJson = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""post"": { ""requestBody"": { ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"", ""properties"": { ""status"": { ""type"": ""string"", ""enum"": [""active"", ""inactive""] } } } } } } } } } }";
        var newJson = @"{ ""openapi"": ""3.0.0"", ""paths"": { ""/users"": { ""post"": { ""requestBody"": { ""content"": { ""application/json"": { ""schema"": { ""type"": ""object"", ""properties"": { ""status"": { ""type"": ""string"", ""enum"": [""active""] } } } } } } } } } }";

        var results = _engine.Compare(oldJson, newJson).ToList();

        Assert.Single(results);
        Assert.Equal("BREAKING: POST /users request field 'status' removed enum value 'inactive'", results[0].Message);
    }
}
