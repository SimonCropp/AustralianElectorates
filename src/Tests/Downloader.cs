﻿using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

static class Downloader
{
    static Downloader()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
    }

    static HttpClient httpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    public static async Task DownloadFile(string targetPath, string requestUri)
    {
        HttpRequestMessage requestMessage = new(HttpMethod.Head, requestUri);

        DateTime remoteLastModified;
        using (var headResponse = await httpClient.SendAsync(requestMessage))
        {
            if (headResponse.ReasonPhrase == "Redirect")
            {
                File.Delete(targetPath);
                return;
            }

            remoteLastModified = headResponse.Content.Headers.LastModified.GetValueOrDefault(DateTimeOffset.UtcNow).UtcDateTime;
        }

        if (File.Exists(targetPath))
        {
            if (remoteLastModified <= File.GetLastWriteTimeUtc(targetPath))
            {
                return;
            }

            File.Delete(targetPath);
        }

        using (var response = await httpClient.GetAsync(requestUri))
        {
            await using var httpStream = await response.Content.ReadAsStreamAsync();
            await using FileStream fileStream = new(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await httpStream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
        }

        File.SetLastWriteTimeUtc(targetPath, remoteLastModified);
    }
}