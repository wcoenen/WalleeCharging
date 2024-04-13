# What does WalleeCharging do?

WalleeCharging is a hobby project created to control an Alfen Eve Single S-line. This is the EVSE ("electric vehicle supply equipment") that I use at home to charge my car. The code should be adaptable to other EVSE systems as well,
as long as they support control an energy management system.

It provides the following features which I was missing in my EVSE:
- limit charging to times when the electricity price (as set by the day-ahead market) is below some treshold
- limit total (three phase) power as measured by the digital electricity meter, taking into account other consumers

I needed these features because if have a dynamically priced electricity contract, and I live in Flanders where a [capacity tariff](https://www.vlaanderen.be/en/moving-housing-and-energy/the-capacity-tariff) is in effect.
To optimize for dynamic prices, I need to shift electricity consumption as much as possible to the the times when the price is low.
But to optimize for the capacity tariff, I would also like to limit the monthly power peak, as each kilowatt of peak power costs about 50 euro/year.
This software helps me to explore the tension between these two concerns.

