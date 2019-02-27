﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace AustralianElectorates
{
    public static partial class DataLoader
    {
        static Assembly assembly;

        static DataLoader()
        {
            assembly = typeof(DataLoader).Assembly;
            using (var stream = assembly.GetManifestResourceStream("electorates.json"))
            {
                Electorates = Serializer.Deserialize<List<Electorate>>(stream);
            }

            foreach (var electorate in Electorates)
            {
                foreach (var member in electorate.Members)
                {
                    member.Electorate = electorate;
                }
            }

            AllMembers = Electorates
                .SelectMany(x => x.Members)
                .ToList();
            AllCurrentMembers = Electorates
                .Where(x => x.Members.Any())
                .Select(x => x.Members.First())
                .ToList();
            InitNamed();
        }

        public static IReadOnlyList<Member> AllMembers { get; }
        public static IReadOnlyList<Member> AllCurrentMembers { get; }

        public static IReadOnlyList<Electorate> Electorates { get; }
        public static MapCollection CurrentMaps { get; } = new MapCollection("Current");
        public static MapCollection FutureMaps { get; } = new MapCollection("Future");

        public static Electorate FindElectorate(string name)
        {
            Guard.AgainstNullWhiteSpace(nameof(name), name);
            if (TryFindElectorate(name, out var electorate))
            {
                return electorate;
            }

            throw new ElectorateNotFoundException(name);
        }

        public static bool TryFindElectorate(string name, out Electorate electorate)
        {
            Guard.AgainstNullWhiteSpace(nameof(name), name);
            electorate = Electorates.SingleOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (electorate != null)
            {
                return true;
            }

            return false;
        }

        public static void ValidateElectorates(params string[] names)
        {
            ValidateElectorates((IEnumerable<string>)names);
        }

        public static void ValidateElectorates(IEnumerable<string> names)
        {
            var missing = FindInvalidateElectorates(names).ToList();
            if (missing.Any())
            {
                throw new ElectoratesNotFoundException(missing);
            }
        }
        public static IEnumerable<string> FindInvalidateElectorates(params string[] names)
        {
           return FindInvalidateElectorates((IEnumerable<string>)names);
        }

        public static IEnumerable<string> FindInvalidateElectorates(IEnumerable<string> names)
        {
            Guard.AgainstNull(names, nameof(names));
            return names.Where(name => !Electorates.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)));
        }

        public static void LoadAll()
        {
            FutureMaps.LoadAll();
            CurrentMaps.LoadAll();
        }

        public static void Export(string directory, bool overwrite = false)
        {
            Guard.AgainstNullWhiteSpace(nameof(directory), directory);
            var electoratesJsonPath = Path.Combine(directory, "electorates.json");
            if (!overwrite && File.Exists(electoratesJsonPath))
            {
                throw new Exception($"File exists: {electoratesJsonPath}");
            }

            using (var stream = assembly.GetManifestResourceStream("electorates.json"))
            using (var target = File.Create(electoratesJsonPath))
            {
                stream.CopyTo(target);
            }

            using (var stream = assembly.GetManifestResourceStream("Maps.zip"))
            using (var archive = new ZipArchive(stream))
            {
                archive.ExtractToDirectory(directory, overwrite);
            }
        }

        public static ElectorateMap GetCurrentMap(this Electorate electorate)
        {
            Guard.AgainstNull(electorate, nameof(electorate));
            if (!electorate.ExistInCurrent)
            {
                throw new Exception($"Electorate '{electorate.Name}' does not have a current map");
            }

            return CurrentMaps.GetElectorate(electorate.ShortName);
        }

        public static ElectorateMap GetFutureMap(this Electorate electorate)
        {
            Guard.AgainstNull(electorate, nameof(electorate));
            if (!electorate.ExistInFuture)
            {
                throw new Exception($"Electorate '{electorate.Name}' does not have a future map");
            }

            return CurrentMaps.GetElectorate(electorate.ShortName);
        }
    }
}