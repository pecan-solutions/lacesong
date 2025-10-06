using Lacesong.Core.Services;
using Microsoft.Extensions.Caching.Memory;

// test the thunderstore service directly
var thunderstoreService = new ThunderstoreService();
var modIndexService = new ThunderstoreModIndexService(thunderstoreService);

Console.WriteLine("Testing ThunderstoreModIndexService...");

try
{
    var criteria = new Lacesong.Core.Models.ModSearchCriteria
    {
        Query = null,
        Category = null,
        Page = 1,
        PageSize = 5,
        SortBy = "rating",
        SortOrder = "desc"
    };

    var results = await modIndexService.SearchMods(criteria);
    Console.WriteLine($"Found {results.Mods.Count} mods");
    
    foreach (var mod in results.Mods.Take(3))
    {
        Console.WriteLine($"- {mod.Name} by {mod.Author}");
        Console.WriteLine($"  Description: {mod.Description}");
        Console.WriteLine($"  Downloads: {mod.DownloadCount}");
        Console.WriteLine($"  Rating: {mod.Rating}");
        Console.WriteLine();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}
