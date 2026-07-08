using System.Collections.Generic;
using NuclearMeltdown.Core;
using Xunit;

public class PollutionGridTests
{
    [Fact]
    public void WorldToCell_maps_origin_to_center()
    {
        Assert.Equal(256, PollutionGrid.WorldToCell(0f));
    }

    [Fact]
    public void WorldToCell_clamps_out_of_range()
    {
        Assert.Equal(0, PollutionGrid.WorldToCell(-100000f));
        Assert.Equal(511, PollutionGrid.WorldToCell(100000f));
    }

    [Fact]
    public void CellIndex_is_row_major()
    {
        Assert.Equal(2 * 512 + 3, PollutionGrid.CellIndex(3, 2));
    }

    [Fact]
    public void CellsInRadius_center_has_max_intensity()
    {
        var cells = PollutionGrid.CellsInRadius(0f, 0f, 700f, 255);
        int centerIndex = PollutionGrid.CellIndex(256, 256);
        var center = cells.Find(c => c.Index == centerIndex);
        Assert.Equal((byte)255, center.Intensity);
    }

    [Fact]
    public void CellsInRadius_excludes_cells_outside_radius()
    {
        // 半径 1セル(33.75m)未満 → 実質中心セルのみ
        var cells = PollutionGrid.CellsInRadius(0f, 0f, 10f, 255);
        Assert.All(cells, c =>
        {
            int cz = c.Index / 512;
            int cx = c.Index % 512;
            Assert.InRange(cx, 255, 257);
            Assert.InRange(cz, 255, 257);
        });
    }

    [Fact]
    public void CellsInRadius_indices_are_unique()
    {
        var cells = PollutionGrid.CellsInRadius(0f, 0f, 300f, 255);
        var seen = new HashSet<int>();
        foreach (var c in cells) Assert.True(seen.Add(c.Index), "duplicate index " + c.Index);
    }
}
