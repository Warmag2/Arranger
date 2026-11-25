using System;
using System.Text.Json;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Server.Hosting;
using Faemiyah.Common;
using NUnit.Framework;

namespace Faemiyah.Arranger.UnitTests;

[TestFixture]
public class SerializationTests
{
    private enum Color
    {
        Red,
    }

    [Test]
    public void DefaultOptions_OmitNullsAndWriteEnumsAsStrings()
    {
        var json = JsonSerializer.Serialize(new { Name = (string?)null, Color = Color.Red }, JsonConfiguration.CreateJsonSerializerOptions());

        json.Should().Be("{\"Color\":\"Red\"}");
    }

    [TestCase(typeof(ArrangementSnapshot), true)]
    [TestCase(typeof(SerializationTests), true)]
    [TestCase(typeof(JsonConfiguration), false)]
    [TestCase(typeof(string), false)]
    [TestCase(typeof(Guid), false)]
    public void IsArrangerType_SelectsFaemiyahArrangerNamespaces(Type type, bool expected)
    {
        OrleansExtensions.IsArrangerType(type).Should().Be(expected);
    }
}
