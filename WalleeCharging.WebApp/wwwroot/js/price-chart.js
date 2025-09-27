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

function clickHandler(click)
{
  const points = chart.getElementsAtEventForMode(click, 'nearest', {intersect: true}, true);
  const clickedPrice = prices[points[0].index];

  // set max price textbox
  document.getElementById('maxPriceEurocentPerMWh').setAttribute('value', clickedPrice);

  // highlight bars representing a value equal to or below the selected one
  highlightBarsWithPriceEqualOrBelow(clickedPrice);
}
chart.canvas.onclick = clickHandler;

function highlightBarsWithPriceEqualOrBelow(selectedPrice) {
    // this clears off any tooltip highlights
    chart.update();
    chart.activeElements = [];

    var backgroundColors = [];
    for (var i=0; i<prices.length; i++) {
        if (prices[i] <= selectedPrice)
        {
            backgroundColors.push('#9ad0f5');
        }
        else
        {
            backgroundColors.push('lightgrey');
        }
    }
  
    var dataset = chart.data.datasets[0];
    dataset.backgroundColor = backgroundColors;
    chart.update();
  }