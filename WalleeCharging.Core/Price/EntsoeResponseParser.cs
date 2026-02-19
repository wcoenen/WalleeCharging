using System.Globalization;
using System.Xml.Linq;

namespace WalleeCharging.Price;

/// <summary>
/// Parses XML responses from the ENTSO-E Transparency Platform API into electricity prices.
/// </summary>
public static class EntsoeResponseParser
{
    private static readonly TimeSpan QUARTER_HOUR = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Parses an XML response from the ENTSO-E Transparency Platform API.
    /// </summary>
    /// <param name="xmlText">The raw XML response text.</param>
    /// <returns>An array of electricity prices parsed from the response.</returns>
    /// <exception cref="InvalidDataException">Thrown when the response contains unexpected or invalid data.</exception>
    /// <exception cref="System.Xml.XmlException">Thrown when the response is not valid XML.</exception>
    /// <exception cref="FormatException">Thrown when numeric values in the response cannot be parsed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when expected XML elements are not found.</exception>
    public static ElectricityPrice[] Parse(string xmlText)
    {
        var xdoc = XDocument.Parse(xmlText);
        if (xdoc.Root == null)
        {
            throw new InvalidDataException();
        }

        // The entso.eu API will sometimes update the "publication document" version embedded in the XML namespace without warning
        // and without managing multiple versions of the API.
        //
        // Example namespaces:
        //      urn:iec62325.351:tc57wg16:451-3:publicationdocument:7:0
        //      urn:iec62325.351:tc57wg16:451-3:publicationdocument:7:3
        //
        // So we have little choice but to accept any namespace.
        string namespaceName = xdoc.Root.Name.Namespace.NamespaceName;

        // Check that there is only one time resolution in the response, and it is PT15M.
        var resolutionElementName = XName.Get("resolution", namespaceName);
        string timeResolution = xdoc.Descendants(resolutionElementName).Single().Value;
        if (timeResolution != "PT15M")
        {
            throw new InvalidDataException($"Expected time resolution 'PT15M' in response from entso.eu API but got '{timeResolution}'");
        }

        // Get the time interval from the response.
        var timeIntervalElementName = XName.Get("period.timeInterval", namespaceName);
        var startElementName = XName.Get("start", namespaceName);
        var endElementName = XName.Get("end", namespaceName);
        var timeIntervalElement = xdoc.Descendants(timeIntervalElementName).Single();
        string? startText = timeIntervalElement.Element(startElementName)?.Value;
        string? endText = timeIntervalElement.Element(endElementName)?.Value;
        if (startText == null || endText == null)
        {
            throw new InvalidDataException("Missing time interval data in response from entso.eu API");
        }
        DateTime startTime = DateTime.Parse(startText, null, DateTimeStyles.RoundtripKind);
        DateTime endTime = DateTime.Parse(endText, null, DateTimeStyles.RoundtripKind);
        if (startTime > endTime)
        {
            throw new InvalidDataException($"Got improper time interval in response from entso.eu API: {startText} to {endText}");
        }

        // Extract price points. Each price point is structured as shown below,
        // with the position representing the exact time in the requested time range.
        //
        // To calculate the time when a price takes effect, we subtract one from the position
        // (to turn it into a proper zero-based offset) and then add that number of quarter-hours to the
        // start of the requested range.
        //
        // Unfortunately, it seems that the API can omit positions if the price doesn't change.
        // For example, if position 23 is missing, then the price of position 22 is in effect
        // for more than one quarter-hour. We fill in such holes.
        //
        // <Point>
        //        <position>1</position>
        //        <price.amount>74.33</price.amount>
        // </Point>

        XName pointElementName = XName.Get("Point", namespaceName);
        XName positionElementName = XName.Get("position", namespaceName);
        XName priceElementName = XName.Get("price.amount", namespaceName);
        List<XElement> pointElements = xdoc.Descendants(pointElementName).ToList();
        var pricePointList = new List<ElectricityPrice>();

        for (int i = 0; i < pointElements.Count; i++)
        {
            // Get the position and price of this point.
            XElement pointElement = pointElements[i];
            XElement? positionElement = pointElement.Element(positionElementName);
            if (positionElement == null)
            {
                throw new InvalidDataException("entso.eu API response has a 'Point' element without a 'position'.");
            }
            int position = Int32.Parse(positionElement.Value);
            var priceElement = pointElement.Element(priceElementName);
            if (priceElement == null)
            {
                throw new InvalidDataException("entso.eu API response has a 'Point' element without a 'price.amount'.");
            }
            decimal priceEuroMWh = decimal.Parse(priceElement.Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            int priceEurocentMWh = (int)(priceEuroMWh * 100);

            // Get the position of the next point, if any.
            XElement? nextPointElement = (i < pointElements.Count - 1) ? pointElements[i + 1] : null;
            XElement? nextPositionElement = nextPointElement?.Element(positionElementName);
            int? nextPosition = (nextPositionElement != null) ? Int32.Parse(nextPositionElement.Value) : null;

            // Check for holes in the data at the start.
            if (i == 0 && position != 1)
            {
                throw new InvalidDataException("entso.eu API response does not start with a 'Point' at position 1");
            }

            // Check for improper order of the points.
            if (nextPosition != null && nextPosition <= position)
            {
                throw new InvalidDataException("entso.eu API response has a 'Point' element with a 'position' that indicates improper order.");
            }

            // Calculate the time when this price point takes effect.
            // This is based on the overall startTime, point position and time resolution.
            DateTime priceStartTime = startTime.Add((position - 1) * QUARTER_HOUR);

            // The price is in effect until the next price point, or until endTime if there is no next point.
            // It is not guarantueed that the next point is one quarter-hour later. Not all positions are required to be present.
            DateTime priceEndTime = (nextPosition != null) ? startTime.Add((nextPosition.Value - 1) * QUARTER_HOUR) : endTime;

            pricePointList.Add(new ElectricityPrice(priceStartTime, priceEndTime, priceEurocentMWh));
        }

        return pricePointList.ToArray();
    }
}
