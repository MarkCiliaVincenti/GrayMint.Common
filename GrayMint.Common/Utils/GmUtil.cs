﻿using System.Security.Cryptography;
using System.Text.Json;

namespace GrayMint.Common.Utils;

public static class GmUtil
{
    public static async Task ForEachAsync<T>(T[] source, Func<T, Task> body, int maxDegreeOfParallelism,
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var t in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            tasks.Add(body(t));
            if (tasks.Count == maxDegreeOfParallelism)
            {
                await Task.WhenAny(tasks);
                foreach (var completedTask in tasks.Where(x => x.IsCompleted).ToArray())
                    tasks.Remove(completedTask);
            }
        }
        await Task.WhenAll(tasks);
    }

    public static T JsonDeserialize<T>(string json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(json, options) ??
               throw new InvalidDataException($"{typeof(T)} could not be deserialized!");
    }

    public static byte[] GenerateKey(int keySize)
    {
        using var aes = Aes.Create();
        aes.KeySize = keySize;
        aes.GenerateKey();
        return aes.Key;
    }
}