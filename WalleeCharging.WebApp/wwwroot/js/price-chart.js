var initializedChart;
var initializedPrices;

// initialize chart
getPricepointsAndPopulateChart();

// chart reacts to changes in max price input field
document.getElementById('maxPriceEurocentPerMWh').addEventListener("change", updateHighlightedBars);

function updateHighlightedBars() {
  const currentMaxPrice = document.getElementById('maxPriceEurocentPerMWh').value;
  highlightBarsWithPriceEqualOrBelow(currentMaxPrice);
}

function getPricepointsAndPopulateChart()
{
  fetch('/api/prices')
    .then(response => response.json())
    .then(pricePoints => PopulateChart(pricePoints));
}

function PopulateChart(pricePoints)
{
  const labels = pricePoints.map(pricePoint=>new Date(pricePoint.time));
  const prices = pricePoints.map(pricePoint=>pricePoint.priceEurocentPerMWh);
  initializedPrices = prices;
  const ctx = document.getElementById('priceChart');
  const chart = new Chart(ctx, {
    type: 'bar',
    data: {
      labels: labels,
      datasets: [{
        label: 'Prices',
        data: prices
      }]
    },
    options: {
        events: ['click'],
        scales: {
          x: {
            ticks: {
              callback: function(value, index, ticks) {
                const date = new Date(this.getLabelForValue(value));
                // Format as YYYY-MM-DD HH:mm
                return date.toLocaleString('en-GB', {
                  hour: '2-digit',
                  minute: '2-digit',
                  hour12: false
                }).replace(',', '');
              }
            }
          },
          y: {
          beginAtZero: true
          }
        }
    }
  });
  initializedChart = chart;
  chart.canvas.onclick = clickHandler;
  updateHighlightedBars();
}

function clickHandler(click)
{
  const points = initializedChart.getElementsAtEventForMode(click, 'nearest', {intersect: true}, true);
  const clickedPrice = initializedPrices[points[0].index];

  // set max price textbox
  document.getElementById('maxPriceEurocentPerMWh').setAttribute('value', clickedPrice);

  // highlight bars representing a value equal to or below the selected one
  highlightBarsWithPriceEqualOrBelow(clickedPrice);
}

function highlightBarsWithPriceEqualOrBelow(selectedPrice) {
    // this clears off any tooltip highlights
    initializedChart.update();
    initializedChart.activeElements = [];

    var backgroundColors = [];
    for (var i=0; i<initializedPrices.length; i++) {
        if (initializedPrices[i] <= selectedPrice)
        {
            backgroundColors.push('#9ad0f5');
        }
        else
        {
            backgroundColors.push('lightgrey');
        }
    }
  
    var dataset = initializedChart.data.datasets[0];
    dataset.backgroundColor = backgroundColors;
    initializedChart.update();
  }

