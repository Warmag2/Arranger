using System;
using System.Text.Json;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Arranger.Server.Hosting;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class SerializationTests
{
    private enum Color
    {
        Red,
    }

    [TestCase(typeof(JsonConfiguration), true)]
    [TestCase(typeof(SerializationTests), true)]
    [TestCase(typeof(string), false)]
    [TestCase(typeof(Guid), false)]
    public void IsArrangerType_SelectsFaemiyahArrangerNamespaces(Type type, bool expected)
    {
        SerializationExtensions.IsArrangerType(type).Should().Be(expected);
    }

    [Test]
    public void DefaultOptions_OmitNullsAndWriteEnumsAsStrings()
    {
        var json = JsonSerializer.Serialize(new { Name = (string?)null, Color = Color.Red }, JsonConfiguration.CreateJsonSerializerOptions());

        json.Should().Be("{\"Color\":\"Red\"}");
    }
}
