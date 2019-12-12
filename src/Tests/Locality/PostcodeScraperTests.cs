﻿using System.IO;
using System.Linq;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

public class PostcodeScraperTests :
    VerifyBase
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Run()
    {
        var data = await PostcodeScraper.Run();
        File.Delete(DataLocations.LocalitiesPath);
        JsonSerializerService.Serialize(data, DataLocations.LocalitiesPath);
        await Verify(data.Take(10));
    }

    [Fact]
    public Task Specific()
    {
        var place = AustraliaData.PostCodes.Single(x=>x.Key == "2612");
        return Verify(PostcodeScraper.GetAECDataForPostcode(place));
    }

    public PostcodeScraperTests(ITestOutputHelper output) :
        base(output)
    {
    }
}