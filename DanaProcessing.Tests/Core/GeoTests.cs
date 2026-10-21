using Xunit;

namespace DanaProcessing.Tests.Core;

/// <summary>The Geo helpers and GeoJSON: places on a sphere, distances, circles, and which country a point is in.</summary>
public class GeoTests
{
    /// <summary>A square "country" with a lake in it, a line, and an earthquake (USGS style: depth as the 3rd coordinate).</summary>
    internal const string SampleCollection = """
    { "type": "FeatureCollection", "features": [
      { "type": "Feature", "id": "sq", "properties": { "NAME": "Squareland", "pop": 1200, "code": "SQ" },
        "geometry": { "type": "Polygon", "coordinates": [
          [[0,0],[10,0],[10,10],[0,10],[0,0]],
          [[4,4],[6,4],[6,6],[4,6],[4,4]] ] } },
      { "type": "Feature", "properties": { "name": "Route" },
        "geometry": { "type": "LineString", "coordinates": [[-5,-5],[-6,-7]] } },
      { "type": "Feature", "id": "us7000abcd", "properties": { "mag": 4.7, "place": "10 km N of Somewhere", "time": 1729500000000, "title": "M 4.7 - Somewhere" },
        "geometry": { "type": "Point", "coordinates": [-71.5, -33.2, 35.4] } }
    ] }
    """;

    [Theory]
    [InlineData(0, 0, 0, 0, -1)]       // longitude 0 faces a camera on -z
    [InlineData(0, 90, 1, 0, 0)]       // 90° east is +x
    [InlineData(90, 0, 0, 1, 0)]       // the north pole is up
    public void ToSphere_puts_places_where_the_doc_says(float lat, float lon, float x, float y, float z)
    {
        var p = Geo.ToSphere(lat, lon, 1);
        Assert.Equal(x, p.X, 4); Assert.Equal(y, p.Y, 4); Assert.Equal(z, p.Z, 4);
        var (la, lo) = Geo.FromSphere(Geo.ToSphere(lat, lon, 7));
        Assert.Equal(lat, la, 3);
        if (Math.Abs(lat) < 89) Assert.Equal(lon, lo, 3);
    }

    [Fact]
    public void Distances_and_destinations_match_the_real_earth()
    {
        // Havana -> Madrid is about 7,440 km on a great circle.
        Assert.InRange(Geo.DistanceKm(23.11f, -82.37f, 40.42f, -3.70f), 7350, 7550);
        // 111 km north of the equator is about 1 degree of latitude.
        var (lat, lon) = Geo.Destination(0, 0, 0, 111.2f);
        Assert.Equal(1, lat, 1); Assert.Equal(0, lon, 3);
        // Every point of a circle is at that distance.
        foreach (var (la, lo) in Geo.Circle(40, -3, 500, 24))
            Assert.InRange(Geo.DistanceKm(40, -3, la, lo), 499, 501);
        Assert.Equal(25, Geo.Circle(40, -3, 500, 24).Length);
    }

    [Fact]
    public void Flat_maps_round_trip()
    {
        var (x, y) = Geo.Equirectangular(0, 0, 360, 180);
        Assert.Equal((180f, 90f), (x, y));
        var (la, lo) = Geo.FromEquirectangular(Geo.Equirectangular(-33, 151, 800, 400).X, Geo.Equirectangular(-33, 151, 800, 400).Y, 800, 400);
        Assert.Equal(-33, la, 3); Assert.Equal(151, lo, 3);
    }

    [Fact]
    public void FibonacciSphere_spreads_points_evenly()
    {
        var pts = Geo.FibonacciSphere(5000);
        Assert.All(pts, p => Assert.Equal(1, p.Mag(), 3));
        // As many in the northern hemisphere as in the southern, and in the east as in the west.
        Assert.InRange(pts.Count(p => p.Y > 0), 2450, 2550);
        Assert.InRange(pts.Count(p => p.X > 0), 2400, 2600);
    }

    [Fact]
    public void GeoJSON_reads_polygons_lines_points_and_properties()
    {
        var map = GeoMap.Parse(SampleCollection);
        Assert.Equal(3, map.Count);

        var land = map.Features[0];
        Assert.Equal("Squareland", land.Name);
        Assert.Equal("sq", land.Id);
        Assert.Equal(1200, land.Number("pop"));
        Assert.Equal("SQ", land.Text("code"));
        Assert.Equal("1200", land.Text("pop"));
        Assert.True(double.IsNaN(land.Number("missing")));
        Assert.Equal(2, land.Rings.Count);
        Assert.True(land.Rings[1].IsHole);
        Assert.Equal((0f, 10f, 0f, 10f), (land.MinLat, land.MaxLat, land.MinLon, land.MaxLon));

        Assert.Equal("LineString", map.Features[1].GeometryType);

        var quake = map.Features[2];
        Assert.Equal("Point", quake.GeometryType);
        Assert.Equal((-33.2f, -71.5f, 35.4f), (quake.Lat, quake.Lon, quake.Alt));
        Assert.Equal(4.7, quake.Number("mag"), 3);
        Assert.Equal(1729500000000, (long)quake.Number("time"));
        Assert.Equal("M 4.7 - Somewhere", quake.Name);
    }

    [Theory]
    [InlineData(2, 2, true)]       // in the country
    [InlineData(5, 5, false)]      // in the lake (a hole)
    [InlineData(12, 5, false)]     // outside
    [InlineData(-5.5f, -6, false)] // on the route: lines contain nothing
    public void Contains_and_FeatureAt_know_which_country_a_place_is_in(float lat, float lon, bool inLand)
    {
        var map = GeoMap.Parse(SampleCollection);
        Assert.Equal(inLand, map.Features[0].Contains(lat, lon));
        Assert.Equal(inLand ? "Squareland" : null, map.FeatureAt(lat, lon)?.Name);
    }

    [Fact]
    public void A_bare_geometry_or_single_feature_parses_too()
    {
        Assert.Equal("MultiPolygon", GeoMap.Parse("""{"type":"MultiPolygon","coordinates":[[[[0,0],[1,0],[1,1],[0,0]]]]}""").Features[0].GeometryType);
        Assert.Equal("A", GeoMap.Parse("""{"type":"Feature","properties":{"name":"A"},"geometry":null}""").Features[0].Name);
    }
}
