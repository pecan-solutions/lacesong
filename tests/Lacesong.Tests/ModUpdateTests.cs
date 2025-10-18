using Lacesong.Core.Services;
using Lacesong.Core.Models;
using Lacesong.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Xunit;

namespace Lacesong.Tests;

public class ModUpdateTests
{
    private static ThunderstoreService CreateService(out Mock<HttpMessageHandler> handlerMock)
    {
        handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                var dto = new ThunderstorePackageDetailDto
                {
                    Namespace = "author",
                    Name = "TestMod",
                    Full_Name = "author-TestMod",
                    Date_Updated = DateTime.UtcNow,
                    Latest = new ThunderstorePackageDetailDto.LatestDto
                    {
                        Version_Number = "1.1.0",
                        Download_Url = "https://example.com/mod.zip",
                        Date_Created = DateTime.UtcNow,
                        Downloads = 100
                    }
                };
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(dto)
                };
                return response;
            });
        var httpClient = new HttpClient(handlerMock.Object);
        return new ThunderstoreService(httpClient, new MemoryCache(new MemoryCacheOptions()), "https://dummy");
    }

    [Fact]
    public async Task GetPackageDetailAsync_CachesInMemory()
    {
        var service = CreateService(out var handlerMock);
        var dto1 = await service.GetPackageDetailAsync("author", "TestMod");
        var dto2 = await service.GetPackageDetailAsync("author", "TestMod");

        Assert.NotNull(dto1);
        Assert.Equal(dto1?.Latest?.Version_Number, "1.1.0");
        // verify only one HTTP call due to cache
        handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        Assert.Same(dto1, dto2);
    }

    [Fact]
    public async Task CheckForUpdates_DetectsNewVersion()
    {
        var tsService = CreateService(out _);
        var modManagerMock = new Mock<IModManager>();
        modManagerMock.Setup(m => m.GetInstalledMods(It.IsAny<GameInstallation>())).ReturnsAsync(new List<ModInfo>
        {
            new ModInfo { Id = "author-TestMod", Version = "1.0.0" }
        });

        var indexServiceMock = new Mock<IModIndexService>(); // not used in new logic
        var configMock = new Mock<IModConfigService>();
        var conflictMock = new Mock<IConflictDetectionService>();
        var verifyMock = new Mock<IVerificationService>();
        var backupMock = new Mock<IBackupManager>();

        var service = new ModUpdateService(indexServiceMock.Object, tsService, modManagerMock.Object, configMock.Object, conflictMock.Object, verifyMock.Object, backupMock.Object);
        var game = new GameInstallation { InstallPath = "/tmp" };
        var updates = await service.CheckForUpdates(game, null, true);
        Assert.Single(updates);
        Assert.Equal("1.1.0", updates[0].AvailableVersion);
    }
}
