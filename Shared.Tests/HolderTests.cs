using System.Collections.Concurrent;
using System.Collections.Generic;
using NintrollerLib;
using WiinUSoft.Holders;
using Xunit;

namespace Shared.Tests;

public class HolderTests
{
    [Fact]
    public void SetValueAddsAndUpdatesOnlyMappedInputs()
    {
        var holder = new TestHolder();
        holder.SetMapping("strum", "A");

        holder.SetValue("strum", -0.75f);
        Assert.Equal(0.75f, holder.Values["strum"]);

        holder.SetValue("strum", 0.25f);
        Assert.Equal(0.25f, holder.Values["strum"]);

        holder.SetValue("unmapped", 1f);
        Assert.False(holder.Values.ContainsKey("unmapped"));
    }

    [Fact]
    public void MappingAndFlagOperationsPreserveExpectedDefaults()
    {
        var holder = new TestHolder();
        holder.SetMapping("button", "B");
        holder.SetMapping("button", "X");

        Assert.Equal("X", holder.Mappings["button"]);
        Assert.False(holder.GetFlag("missing"));

        holder.Flags["pressed"] = true;
        holder.Flags["released"] = false;
        Assert.True(holder.GetFlag("pressed"));
        Assert.False(holder.GetFlag("released"));

        holder.ClearMapping("button");
        Assert.False(holder.Mappings.ContainsKey("button"));
    }

    private sealed class TestHolder : Holder
    {
        public TestHolder()
        {
            Values = new ConcurrentDictionary<string, float>();
            Mappings = new Dictionary<string, string>();
            Flags = new Dictionary<string, bool>();
        }

        public override void Update() { }

        public override void Close() { }

        public override void AddMapping(ControllerType controller) { }
    }
}
