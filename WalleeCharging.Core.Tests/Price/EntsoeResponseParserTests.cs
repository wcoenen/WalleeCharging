using WalleeCharging.Price;

namespace WalleeCharging.Core.Tests.Price;

[TestClass]
public class EntsoeResponseParserTests
{
    private static string ReadTestVector(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Price", "EntsoeResponses", fileName);
        return File.ReadAllText(path);
    }

    // 2025-10-01 in Belgium (CEST, UTC+2): the API returns UTC, so midnight local = 22:00Z the previous day.
    [TestMethod]
    public void Parse_20251001_Returns96QuarterHourPrices()
    {
        string xml = ReadTestVector("entsoe_20251001.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        Assert.HasCount(96, prices);
    }

    [TestMethod]
    public void Parse_20251001_AllIntervalsAre15MinutesAndContiguous()
    {
        string xml = ReadTestVector("entsoe_20251001.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        TimeSpan quarterHour = TimeSpan.FromMinutes(15);
        for (int i = 0; i < prices.Length; i++)
        {
            Assert.AreEqual(quarterHour, prices[i].EndTime - prices[i].StartTime,
                $"prices[{i}] duration is not 15 minutes");
            if (i > 0)
            {
                Assert.AreEqual(prices[i - 1].EndTime, prices[i].StartTime,
                    $"prices[{i}] does not start where prices[{i - 1}] ends");
            }
        }
    }

    [TestMethod]
    public void Parse_20251001_FirstPriceIsCorrect()
    {
        string xml = ReadTestVector("entsoe_20251001.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        // Position 1: price.amount 102.68 EUR/MWh = 10268 eurocent/MWh
        Assert.AreEqual(new DateTime(2025, 9, 30, 22, 0, 0, DateTimeKind.Utc), prices[0].StartTime);
        Assert.AreEqual(new DateTime(2025, 9, 30, 22, 15, 0, DateTimeKind.Utc), prices[0].EndTime);
        Assert.AreEqual(10268, prices[0].PriceEurocentPerMWh);
    }

    [TestMethod]
    public void Parse_20251001_LastPriceIsCorrect()
    {
        string xml = ReadTestVector("entsoe_20251001.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        // Position 96: price.amount 80.61 EUR/MWh = 8061 eurocent/MWh
        Assert.AreEqual(new DateTime(2025, 10, 1, 21, 45, 0, DateTimeKind.Utc), prices[95].StartTime);
        Assert.AreEqual(new DateTime(2025, 10, 1, 22, 0, 0, DateTimeKind.Utc), prices[95].EndTime);
        Assert.AreEqual(8061, prices[95].PriceEurocentPerMWh);
    }

    // 2025-09-30: the day-ahead market was still hourly. The API reports PT15M resolution but
    // only provides 24 price points (positions 1, 5, 9, …, 93 — stride 4). The gap-filling
    // logic in the parser extends each price to cover the full hour until the next point.
    // The last point (position 93) has no successor, so it gets only 15 minutes.

    [TestMethod]
    public void Parse_20250930_Returns24Prices()
    {
        string xml = ReadTestVector("entsoe_20250930.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        Assert.HasCount(24, prices);
    }

    [TestMethod]
    public void Parse_20250930_EachHourlyPriceSpansOneHourAndIsContiguous()
    {
        string xml = ReadTestVector("entsoe_20250930.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        TimeSpan oneHour = TimeSpan.FromHours(1);
        for (int i = 0; i < prices.Length - 1; i++)
        {
            Assert.AreEqual(oneHour, prices[i].EndTime - prices[i].StartTime,
                $"prices[{i}] duration is not 1 hour");
            Assert.AreEqual(prices[i].EndTime, prices[i + 1].StartTime,
                $"prices[{i}] and prices[{i + 1}] are not contiguous");
        }
    }

    [TestMethod]
    public void Parse_20250930_LastPriceStaysInEffectUntilEndTime()
    {
        string xml = ReadTestVector("entsoe_20250930.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        // Position 93 is the last point; with no following position the parser
        // extends it to the period endTime (2025-09-30T22:00Z), covering a full hour.
        Assert.AreEqual(TimeSpan.FromHours(1), prices[23].EndTime - prices[23].StartTime);
    }

    [TestMethod]
    public void Parse_20250930_FirstPriceIsCorrect()
    {
        string xml = ReadTestVector("entsoe_20250930.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        // Position 1, next position 5: covers 2025-09-29T22:00Z–23:00Z, price 86.08 EUR/MWh
        Assert.AreEqual(new DateTime(2025, 9, 29, 22, 0, 0, DateTimeKind.Utc), prices[0].StartTime);
        Assert.AreEqual(new DateTime(2025, 9, 29, 23, 0, 0, DateTimeKind.Utc), prices[0].EndTime);
        Assert.AreEqual(8608, prices[0].PriceEurocentPerMWh);
    }

    [TestMethod]
    public void Parse_20250930_LastPriceIsCorrect()
    {
        string xml = ReadTestVector("entsoe_20250930.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        // Position 93 (index 23): 2025-09-30T21:00Z–22:00Z, price 88.70 EUR/MWh
        Assert.AreEqual(new DateTime(2025, 9, 30, 21, 0, 0, DateTimeKind.Utc), prices[23].StartTime);
        Assert.AreEqual(new DateTime(2025, 9, 30, 22, 0, 0, DateTimeKind.Utc), prices[23].EndTime);
        Assert.AreEqual(8870, prices[23].PriceEurocentPerMWh);
    }

    [TestMethod]
    public void Parse_20250930_PeakPriceIsCorrect()
    {
        string xml = ReadTestVector("entsoe_20250930.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        // Position 77 (index 19): 2025-09-30T17:00Z–18:00Z, price 206.59 EUR/MWh
        Assert.AreEqual(new DateTime(2025, 9, 30, 17, 0, 0, DateTimeKind.Utc), prices[19].StartTime);
        Assert.AreEqual(new DateTime(2025, 9, 30, 18, 0, 0, DateTimeKind.Utc), prices[19].EndTime);
        Assert.AreEqual(20659, prices[19].PriceEurocentPerMWh);
    }

    [TestMethod]
    public void Parse_20251001_PeakPriceIsCorrect()
    {
        string xml = ReadTestVector("entsoe_20251001.xml");

        ElectricityPrice[] prices = EntsoeResponseParser.Parse(xml);

        // Position 77 (index 76): price.amount 340.79 EUR/MWh = 34079 eurocent/MWh
        // startTime = 2025-09-30T22:00Z + 76 * 15min = 2025-10-01T17:00Z
        Assert.AreEqual(new DateTime(2025, 10, 1, 17, 0, 0, DateTimeKind.Utc), prices[76].StartTime);
        Assert.AreEqual(new DateTime(2025, 10, 1, 17, 15, 0, DateTimeKind.Utc), prices[76].EndTime);
        Assert.AreEqual(34079, prices[76].PriceEurocentPerMWh);
    }
}
