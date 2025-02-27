using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cape_Town_Festival.Services
{
    public class WeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WeatherService> _logger;

        public WeatherService(IConfiguration configuration, ILogger<WeatherService> logger)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
            _logger = logger;
            _apiKey = _configuration["OpenWeatherMap:ApiKey"];
            
            // Log the API key (partially masked for security)
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("OpenWeatherMap API key is missing!");
            }
            else
            {
                string maskedKey = _apiKey.Length > 4 
                    ? _apiKey.Substring(0, 4) + "..." 
                    : "[empty]";
                _logger.LogInformation($"OpenWeatherMap API key loaded: {maskedKey}");
            }
        }

       public async Task<WeatherData> GetWeatherForLocationAsync(string location, DateTime eventDate)
{
    try
    {
        _logger.LogInformation($"Getting weather for location: '{location}', date: {eventDate}");
        
        // Handle different location formats
        string lat, lon;
        
        // Try to parse as lat,lon format
        if (location.Contains(","))
        {
            var parts = location.Split(',');
            if (parts.Length == 2)
            {
                lat = parts[0].Trim();
                lon = parts[1].Trim();
                _logger.LogInformation($"Using coordinates: {lat}, {lon}");
                
                // If event date is within 5 days, use the forecast API
                if ((eventDate - DateTime.Now).TotalDays <= 5)
                {
                    return await GetForecastWeatherAsync(lat, lon, eventDate);
                }
                else
                {
                    return await GetHistoricalAverageWeatherAsync(location, eventDate);
                }
            }
            else
            {
                _logger.LogWarning($"Could not parse location coordinates: '{location}', using seasonal data");
                return await GetHistoricalAverageWeatherAsync(location, eventDate);
            }
        }
        else
        {
            // If not lat,lon format, use seasonal data
            _logger.LogWarning($"Location not in lat,lon format: '{location}', using seasonal data");
            return await GetHistoricalAverageWeatherAsync(location, eventDate);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error getting weather data: {ex.Message}");
        return new WeatherData
        {
            Temperature = 0,
            Description = "Weather data unavailable",
            Icon = "unknown"
        };
    }
}

private async Task<WeatherData> GetForecastWeatherAsync(string lat, string lon, DateTime eventDate)
{
    try
    {
        // Get 5 day forecast data
        string url = $"https://api.openweathermap.org/data/2.5/forecast?lat={lat}&lon={lon}&units=metric&appid={_apiKey}";
        
        _logger.LogInformation($"Calling forecast API: {url}");
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var forecastData = JObject.Parse(content);

        // Find the forecast closest to the event date
        var forecasts = forecastData["list"];
        JToken closestForecast = null;
        double closestTimeDiff = double.MaxValue;

        foreach (var forecast in forecasts)
        {
            var forecastTime = DateTimeOffset.FromUnixTimeSeconds(forecast["dt"].Value<long>()).DateTime;
            var timeDiff = Math.Abs((forecastTime - eventDate).TotalHours);
            
            if (timeDiff < closestTimeDiff)
            {
                closestTimeDiff = timeDiff;
                closestForecast = forecast;
            }
        }

        if (closestForecast != null)
        {
            _logger.LogInformation($"Found forecast data for {eventDate}");
            return new WeatherData
            {
                Temperature = Math.Round(closestForecast["main"]["temp"].Value<double>()),
                Description = closestForecast["weather"][0]["description"].Value<string>(),
                Icon = closestForecast["weather"][0]["icon"].Value<string>()
            };
        }

        _logger.LogWarning("No forecast data found, using seasonal average");
        return await GetHistoricalAverageWeatherAsync("", eventDate);
    }
    catch (Exception ex)
    {
        _logger.LogError($"Error in forecast API: {ex.Message}");
        return await GetHistoricalAverageWeatherAsync("", eventDate);
    }
}

        private async Task<WeatherData> GetForecastWeatherAsync(string location, DateTime eventDate)
        {
            // Extract coordinates from location string (assuming format like "-33.9249,18.4241")
            var coordinates = location.Split(',');
            if (coordinates.Length != 2)
            {
                throw new ArgumentException("Invalid location format. Expected 'latitude,longitude'");
            }

            string lat = coordinates[0].Trim();
            string lon = coordinates[1].Trim();

            // Get 5 day forecast data
            string url = $"https://api.openweathermap.org/data/2.5/forecast?lat={lat}&lon={lon}&units=metric&appid={_apiKey}";
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var forecastData = JObject.Parse(content);

            // Find the forecast closest to the event date
            var forecasts = forecastData["list"];
            JToken closestForecast = null;
            double closestTimeDiff = double.MaxValue;

            foreach (var forecast in forecasts)
            {
                var forecastTime = DateTimeOffset.FromUnixTimeSeconds(forecast["dt"].Value<long>()).DateTime;
                var timeDiff = Math.Abs((forecastTime - eventDate).TotalHours);
                
                if (timeDiff < closestTimeDiff)
                {
                    closestTimeDiff = timeDiff;
                    closestForecast = forecast;
                }
            }

            if (closestForecast != null)
            {
                return new WeatherData
                {
                    Temperature = Math.Round(closestForecast["main"]["temp"].Value<double>()),
                    Description = closestForecast["weather"][0]["description"].Value<string>(),
                    Icon = closestForecast["weather"][0]["icon"].Value<string>()
                };
            }

            throw new Exception("No forecast data found");
        }

        private async Task<WeatherData> GetHistoricalAverageWeatherAsync(string location, DateTime eventDate)
        {
            // For events beyond the 5-day forecast, we'll return a seasonal average
            // In a real app, you might want to use a paid API for historical data or calculate your own averages
            
            // As a simple implementation, we'll return seasonal averages for Cape Town
            int month = eventDate.Month;

            // Seasonal averages for Cape Town (approximated)
            if (month >= 12 || month <= 2) // Summer (Dec-Feb)
            {
                return new WeatherData
                {
                    Temperature = 26,
                    Description = "Typically sunny",
                    Icon = "01d" // clear sky
                };
            }
            else if (month >= 3 && month <= 5) // Autumn (Mar-May)
            {
                return new WeatherData
                {
                    Temperature = 22,
                    Description = "Partly cloudy",
                    Icon = "02d" // few clouds
                };
            }
            else if (month >= 6 && month <= 8) // Winter (Jun-Aug)
            {
                return new WeatherData
                {
                    Temperature = 18,
                    Description = "Chance of rain",
                    Icon = "10d" // rain
                };
            }
            else // Spring (Sep-Nov)
            {
                return new WeatherData
                {
                    Temperature = 20,
                    Description = "Mild conditions",
                    Icon = "03d" // scattered clouds
                };
            }
        }

                // Add this method to WeatherService class
                public async Task<WeatherData> TestDirectApiCall()
                {
                    try
                    {
                        // Test with Cape Town coordinates
                        string lat = "-33.9249";
                        string lon = "18.4241";
                        string url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&units=metric&appid={_apiKey}";
                        
                        _logger.LogInformation($"Making test API call to: {url}");
                        
                        var response = await _httpClient.GetAsync(url);
                        var content = await response.Content.ReadAsStringAsync();
                        
                        _logger.LogInformation($"API Response: {content}");
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var data = JObject.Parse(content);
                            return new WeatherData
                            {
                                Temperature = Math.Round(data["main"]["temp"].Value<double>()),
                                Description = data["weather"][0]["description"].Value<string>(),
                                Icon = data["weather"][0]["icon"].Value<string>()
                            };
                        }
                        else
                        {
                            _logger.LogError($"API returned error: {response.StatusCode}");
                            _logger.LogError($"Response content: {content}");
                            return new WeatherData
                            {
                                Temperature = 0,
                                Description = $"API Error: {response.StatusCode}",
                                Icon = "unknown"
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Exception in test call: {ex.Message}");
                        _logger.LogError($"Stack trace: {ex.StackTrace}");
                        return new WeatherData
                        {
                            Temperature = 0,
                            Description = $"Error: {ex.Message}",
                            Icon = "unknown"
                        };
                    }
                }

    }



    public class WeatherData
    {
        public double Temperature { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }

        // For convenience, provide the full icon URL
        public string IconUrl => $"https://openweathermap.org/img/wn/{Icon}@2x.png";
    }

    

}