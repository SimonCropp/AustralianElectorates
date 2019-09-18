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
        static object exportLocker = new object();
        static Assembly assembly;

        static DataLoader()
        {
            assembly = typeof(DataLoader).Assembly;
            using (var stream = assembly.GetManifestResourceStream("electorates.json"))
            {
                Electorates = Serializer.Deserialize<List<Electorate>>(stream);
            }
            using (var stream = assembly.GetManifestResourceStream("parties.json"))
            {
                Parties = Serializer.Deserialize<List<Party>>(stream);
            }

            var partiesAndBranches = new List<IParty>();
            foreach (var party in Parties)
            {
                partiesAndBranches.Add(party);
                if (party.Branches != null)
                {
                    foreach (var branch in party.Branches)
                    {
                        partiesAndBranches.Add(branch);
                        branch.Party = party;
                    }
                }
            }

            PartiesAndBranches = partiesAndBranches;

            foreach (var electorate in Electorates)
            {
                var preferred = electorate.TwoCandidatePreferred;
                if (preferred != null)
                {
                    preferred.Elected.Party = PartiesAndBranches.SingleOrDefault(x => x.Id == preferred.Elected.PartyId);
                    electorate.CurrentParty = preferred.Elected.Party;

                    preferred.Other.Party = PartiesAndBranches.SingleOrDefault(x => x.Id == preferred.Other.PartyId);
                }

                foreach (var member in electorate.Members)
                {
                    member.Electorate = electorate;
                    if (member.PartyIds == null)
                    {
                        member.partyIds = new List<ushort>();
                        member.parties = new List<IParty>();
                    }
                    else
                    {
                        member.parties = member.PartyIds
                            .Select(x => PartiesAndBranches.SingleOrDefault(y => y.Id == x))
                            .Where(x => x != null)
                            .ToList();
                    }
                }

                electorate.CurrentMember = electorate.Members.FirstOrDefault();
            }

            AllMembers = Electorates
                .SelectMany(x => x.Members)
                .ToList();
            AllCurrentMembers = Electorates
                .Where(x => x.Members.Any())
                .Select(x => x.Members.First())
                .ToList();
            InitNamed();
            Elections = BuildElections();
        }

        public static IReadOnlyList<Member> AllMembers { get; }
        public static IReadOnlyList<Member> AllCurrentMembers { get; }

        public static IReadOnlyList<Electorate> Electorates { get; }


        public static IReadOnlyList<Election> Elections { get; }

        static List<Election> BuildElections()
        {
            //TODO: scrape from here instead, will need to change from the electorate.ExistNNNN pattern: https://www.aec.gov.au/Elections/Federal_Elections/
            #region elections

            return new List<Election>
            {
                new Election
                {
                    Parliament = 45,
                    Year = 2016,
                    Date = new DateTime(2016, 07, 02, 0, 0, 0),
                    Electorates = Electorates.Where(electorate => electorate.Exist2016).ToList()
                },
                new Election
                {
                    Parliament = 46,
                    Year = 2019,
                    Date = new DateTime(2019, 05, 18, 0, 0, 0),
                    Electorates = Electorates.Where(electorate => electorate.Exist2019).ToList()
                }
            };

            #endregion
        }

        public static IReadOnlyList<Party> Parties { get; }
        public static IReadOnlyList<IParty> PartiesAndBranches { get; }
        public static MapCollection Maps2016 { get; } = new MapCollection("2016");
        public static MapCollection Maps2019 { get; } = new MapCollection("2019");
        public static MapCollection MapsFuture { get; } = new MapCollection("Future");

        public static Election FindElection(int parliament)
        {
            if (TryFindElection(parliament, out var election))
            {
                return election;
            }

            throw new ElectionNotFoundException(parliament);
        }

        public static bool TryFindElection(int parliament, out Election election)
        {
            election = Elections.SingleOrDefault(x => x.Parliament == parliament);
            if (election != null)
            {
                return true;
            }

            return false;
        }

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
            electorate = Electorates.SingleOrDefault(x => MatchName(name, x));
            if (electorate != null)
            {
                return true;
            }

            return false;
        }

        static bool MatchName(string name, Electorate x)
        {
            return string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(x.ShortName, name, StringComparison.OrdinalIgnoreCase);
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

        public static bool TryFindInvalidateElectorates(IEnumerable<string> names, out List<string> invalid)
        {
            Guard.AgainstNull(names, nameof(names));
            invalid = FindInvalidateElectorates(names).ToList();
            return invalid.Any();
        }

        public static IEnumerable<string> FindInvalidateElectorates(params string[] names)
        {
           return FindInvalidateElectorates((IEnumerable<string>)names);
        }

        public static IEnumerable<string> FindInvalidateElectorates(IEnumerable<string> names)
        {
            Guard.AgainstNull(names, nameof(names));
            return names.Where(name => !Electorates.Any(x => MatchName(name, x)));
        }

        public static void LoadAll()
        {
            MapsFuture.LoadAll();
            Maps2016.LoadAll();
            Maps2019.LoadAll();
        }

        public static void Export(string directory)
        {
            Guard.AgainstNullWhiteSpace(nameof(directory), directory);
            lock (exportLocker)
            {
                ExportInLock(directory);
            }
        }

        static void ExportInLock(string directory)
        {
            WriteElectoratesJson(directory);

            using var stream = assembly.GetManifestResourceStream("Maps.zip");
            using var archive = new ZipArchive(stream);
            archive.ExtractToDirectory(directory);
        }

        static void WriteElectoratesJson(string directory)
        {
            var electoratesJsonPath = Path.Combine(directory, "electorates.json");
            if (File.Exists(electoratesJsonPath))
            {
                var existingCreationTime = File.GetCreationTimeUtc(electoratesJsonPath);
                if (AssemblyTimestamp.Value == existingCreationTime)
                {
                    return;
                }
            }

            WriteElectoratesJsonInner(electoratesJsonPath);

            File.SetCreationTimeUtc(electoratesJsonPath, AssemblyTimestamp.Value);
        }

        static void WriteElectoratesJsonInner(string electoratesJsonPath)
        {
            using var stream = assembly.GetManifestResourceStream("electorates.json");
            using var target = File.Create(electoratesJsonPath);
            stream.CopyTo(target);
        }

        public static ElectorateMap Get2016Map(this Electorate electorate)
        {
            Guard.AgainstNull(electorate, nameof(electorate));
            if (!electorate.Exist2016)
            {
                throw new Exception($"Electorate '{electorate.Name}' does not have a 2016 map");
            }

            return Maps2016.GetElectorate(electorate.ShortName);
        }

        public static ElectorateMap Get2019Map(this Electorate electorate)
        {
            Guard.AgainstNull(electorate, nameof(electorate));
            if (!electorate.Exist2019)
            {
                throw new Exception($"Electorate '{electorate.Name}' does not have a 2019 map");
            }

            return Maps2019.GetElectorate(electorate.ShortName);
        }

        public static ElectorateMap GetFutureMap(this Electorate electorate)
        {
            Guard.AgainstNull(electorate, nameof(electorate));
            if (!electorate.ExistInFuture)
            {
                throw new Exception($"Electorate '{electorate.Name}' does not have a future map");
            }

            return MapsFuture.GetElectorate(electorate.ShortName);
        }
    }
}