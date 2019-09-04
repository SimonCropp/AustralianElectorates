﻿using System.IO;
using System;
using System.Runtime.CompilerServices;

static class GitRepoDirectoryFinder
{
    public static string FindForFilePath([CallerFilePath] string sourceFilePath = "")
    {
        var directory = Path.GetDirectoryName(sourceFilePath);
        if (!TryFind(directory, out var rootDirectory))
        {
            throw new Exception("Could not find git repository directory");
        }

        return rootDirectory;
    }

    static bool TryFind(string directory, out string path)
    {
        do
        {
            if (Directory.Exists(Path.Combine(directory, ".git")))
            {
                path = directory;
                return true;
            }

            var parent = Directory.GetParent(directory);
            if (parent == null)
            {
                path = "";
                return false;
            }

            directory = parent.FullName;
        } while (true);
    }
}