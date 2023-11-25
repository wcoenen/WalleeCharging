using System.Configuration;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;

namespace WalleeCharging.Meter;

public class HomeWizardMeterDataProvider : IMeterDataProvider, IDisposable
{
    private readonly string _url;
    private readonly HttpClient _httpClient;

    public HomeWizardMeterDataProvider(string url)
    {
        _url = url;
        _httpClient = new HttpClient();
    }

    public async Task<MeterData> GetMeterDataAsync()
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(_url);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            throw new MeterDataException(
                $"Homewizard API request to {_url} failed.", e);
        }
        
        // parse the response
        string responseBody = await response.Content.ReadAsStringAsync();
        try
        {
            dynamic? data = JsonConvert.DeserializeObject(responseBody);
            
            if (data == null)
                throw new MeterDataException(
                    $"Homewizard API request to {_url} seemed succesfull but returned an empty response");
        

            return new MeterData() 
            {
                TotalActivePower = data.active_power_w,
                Current1 = data.active_current_l1_a,
                Current2 = data.active_current_l2_a,
                Current3 = data.active_current_l3_a,
                Voltage1 = data.active_voltage_l1_v,
                Voltage2 = data.active_voltage_l2_v,
                Voltage3 = data.active_voltage_l3_v,
            };
        }
        catch (Exception e) when (e is JsonSerializationException || e is RuntimeBinderException)
        {
            throw new MeterDataException(
                $"Homewizard API response to {_url} did not contain the expected data. Response body follows.\n{responseBody}", e);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }


}
