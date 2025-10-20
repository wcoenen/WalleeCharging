function increaseMaxPrice() {
    const input = document.getElementById("maxPriceEurocentPerMWh");
    let currentValue = parseInt(input.value);

    // get prices in chart
    const prices = currentChart.prices;
    
    // sort prices ascending
    const sortedPrices = [...new Set(prices)].sort((a, b) => a - b);

    // find the next higher price
    for (let price of sortedPrices) {
        if (price > currentValue) {
            input.value = price;
            updateHighlightedBars();
            return;
        }
    }
}

function decreaseMaxPrice() {
    const input = document.getElementById("maxPriceEurocentPerMWh");
    let currentValue = parseInt(input.value);

    // get prices in chart
    const prices = currentChart.prices;

    // sort prices descending
    const sortedPrices = [...new Set(prices)].sort((a, b) => b - a);
    
    // find the next lower price
    for (let price of sortedPrices) {
        if (price < currentValue) {
            input.value = price;
            updateHighlightedBars();
            return;
        }
    }
}

function setMaxPriceToPercentile(percentile) {
    
    const input = document.getElementById("maxPriceEurocentPerMWh");
    let currentValue = parseInt(input.value);
    
    if (percentile < 0 || percentile > 1) {
        console.error("Percentile must be between 0 and 1");
        return;
    }

    // get prices in chart
    const prices = currentChart.prices;

    // sort prices ascending
    const sortedPrices = [...new Set(prices)].sort((a, b) => a - b);
    const len = sortedPrices.length;
    const index = Math.min(
        Math.floor(len * percentile),
        len-1)
    const percentilePrice = sortedPrices[index];
    input.value = percentilePrice;
    updateHighlightedBars();
}