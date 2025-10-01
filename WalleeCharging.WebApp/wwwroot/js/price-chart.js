var currentChart;

// initialize chart
getPricepointsAndPopulateChart();

// Schedule chart refresh at the start of every quarter-hour
function scheduleQuarterHourRefresh() {
  const now = new Date();
  const minutes = now.getMinutes();
  const seconds = now.getSeconds();
  const ms = now.getMilliseconds();
  // Calculate ms until next quarter-hour (0, 15, 30, 45)
  const nextQuarter = 15 * Math.ceil((minutes + 1) / 15);
  const nextQuarterMinutes = nextQuarter === 60 ? 0 : nextQuarter;
  const addHour = nextQuarter === 60 ? 1 : 0;
  const next = new Date(now.getFullYear(), now.getMonth(), now.getDate(), now.getHours() + addHour, nextQuarterMinutes, 0, 0);
  const delay = next - now;
  setTimeout(() => {
    getPricepointsAndPopulateChart();
    // After first run, refresh every 15 minutes
    setInterval(getPricepointsAndPopulateChart, 15 * 60 * 1000);
  }, delay);
}

scheduleQuarterHourRefresh();

// chart reacts to changes in max price input field
document.getElementById('maxPriceEurocentPerMWh').addEventListener("change", updateHighlightedBars);

function updateHighlightedBars() {
  const currentMaxPrice = document.getElementById('maxPriceEurocentPerMWh').value;
  highlightBarsWithPriceEqualOrBelow(currentMaxPrice);
}

async function getPricepointsAndPopulateChart()
{
  try {
    const response = await fetch('/api/prices');
    const pricePoints = await response.json();
    PopulateChart(pricePoints);
  }
  catch (error) {
    console.error('Error fetching price points:', error);
  }
}

function PopulateChart(pricePoints)
{
  const labels = pricePoints.map(pricePoint=>new Date(pricePoint.time));
  const prices = pricePoints.map(pricePoint=>pricePoint.priceEurocentPerMWh);
  const ctx = document.getElementById('priceChart');
  if (currentChart) {
    currentChart.destroy();
    currentChart = null;
  }
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
  currentChart = chart;
  chart.prices = prices // attach prices to chart for later reference in clickHandler
  chart.canvas.onclick = clickHandler;
  updateHighlightedBars();
}

function clickHandler(click)
{
  const points = currentChart.getElementsAtEventForMode(click, 'nearest', {intersect: true}, true);
  const clickedPrice = currentChart.prices[points[0].index];

  // set max price textbox
  document.getElementById('maxPriceEurocentPerMWh').setAttribute('value', clickedPrice);

  // highlight bars representing a value equal to or below the selected one
  highlightBarsWithPriceEqualOrBelow(clickedPrice);
}

function highlightBarsWithPriceEqualOrBelow(selectedPrice) {
    // this clears off any tooltip highlights
    currentChart.update();
    currentChart.activeElements = [];

    var backgroundColors = [];
    for (var i=0; i<currentChart.prices.length; i++) {
        if (currentChart.prices[i] <= selectedPrice)
        {
            backgroundColors.push('#9ad0f5');
        }
        else
        {
            backgroundColors.push('lightgrey');
        }
    }
  
    var dataset = currentChart.data.datasets[0];
    dataset.backgroundColor = backgroundColors;
    currentChart.update();
}

