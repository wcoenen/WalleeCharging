getApplianceHintsAndPopulate();

async function getApplianceHintsAndPopulate()
{
  try {
    const response = await fetch('/api/hints');
    const hints = await response.json();
    const hintsContainer = document.getElementById('applianceHints');
    hintsContainer.innerHTML = '';
    hints.forEach(hint => {
      const listItem = document.createElement('li');
      listItem.textContent = `${hint.name}: start at ${new Date(hint.optimalStartTime).toLocaleString('en-GB', {
        hour: '2-digit',
        minute: '2-digit',
        hour12: false
      })} for an expected cost of ${hint.expectedTotalCostEuro.toFixed(2)} euro`;
      hintsContainer.appendChild(listItem);
    });
  }
  catch (error) {
    console.error('Error fetching price points:', error);
  }
}