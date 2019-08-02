﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AustralianElectorates;
using HtmlAgilityPack;
using Xunit;

public static class ElectoratesScraper
{
    public static async Task<Electorate> ScrapeCurrentElectorate(string shortName, State state, List<Elected> elected)
    {
        var requestUri = $"https://www.aec.gov.au/profiles/{state}/{shortName}.htm";
        return await ScrapeElectorate(shortName, state, requestUri, "Profile of the electoral division of ", elected);
    }

    public static async Task<Electorate> Scrape2016Electorate(string shortName, State state)
    {
        var requestUri = $"https://www.aec.gov.au/Elections/federal_elections/2016/profiles/{state}/{shortName}.htm";
        return await ScrapeElectorate(shortName, state, requestUri, "2016 federal election: profile of the electoral division of ", new List<Elected>());
    }

    static async Task<Electorate> ScrapeElectorate(string shortName, State state, string requestUri, string prefix, List<Elected> elected)
    {
        //TODO: should not need elected here. should be able to use 2candidatepref
        requestUri = requestUri.ToLowerInvariant();
        var tempElectorateHtmlPath = Path.Combine(DataLocations.TempPath, $"{shortName}.html");
        try
        {
            await Downloader.DownloadFile(tempElectorateHtmlPath, requestUri);

            if (!File.Exists(tempElectorateHtmlPath))
            {
                throw new Exception($"Could not download {shortName}");
            }

            var document = new HtmlDocument();
            document.Load(tempElectorateHtmlPath);

            var fullName = GetFullName(document, prefix);
            var values = new Dictionary<string, HtmlNode>(StringComparer.OrdinalIgnoreCase);
            var profileId = FindProfileTable(document);
            var htmlNodeCollection = profileId.SelectNodes("dt");
            foreach (var keyNode in htmlNodeCollection)
            {
                var valueNode = keyNode.NextSibling.NextSibling;
                values[keyNode.InnerText.Trim().Trim(':').Replace("  ", " ")] = valueNode;
            }

            var contest = MediaFeedService.HouseOfReps.Contests.SingleOrDefault(x => x.ContestIdentifier.ContestName == fullName);
            var electorate = new Electorate
            {
                Name = fullName,
                ShortName = shortName,
                State = state
            };
            if (contest != null)
            {
                electorate.Enrollment = contest.Enrolment.Value;
                var candidatePreferred = contest.TwoCandidatePreferred;
                var electedCandidate = candidatePreferred.Candidate.Single(x => x.Elected.Value);
                var electedCandidateName = SplitName(electedCandidate.CandidateIdentifier.CandidateName);

                var other = candidatePreferred.Candidate.Single(x => !x.Elected.Value);
                var otherName = SplitName(other.CandidateIdentifier.CandidateName);

                electorate.TwoCandidatePreferred = new TwoCandidatePreferred
                {
                    Elected = new Candidate
                    {
                        FamilyName = electedCandidateName.familyName,
                        GivenNames = electedCandidateName.givenNames,
                        Party = electedCandidate.AffiliationIdentifier?.ShortCode,
                        Votes = electedCandidate.Votes.Value,
                        Swing = electedCandidate.Votes.Swing,
                    },
                    Other = new Candidate
                    {
                        FamilyName = otherName.familyName,
                        GivenNames = otherName.givenNames,
                        Party = other.AffiliationIdentifier?.ShortCode,
                        Votes = other.Votes.Value,
                        Swing = other.Votes.Swing,
                    }
                };
            }

            if (values.TryGetValue("Date this name and boundary was gazetted", out var gazettedHtml))
            {
                electorate.DateGazetted = DateTime.ParseExact(gazettedHtml.InnerText, "d MMMM yyyy", null);
            }

            var electorateMembers = ElectorateMembers(values).ToList();
            var first = electorateMembers.FirstOrDefault();
            if (first != null && first.End == null)
            {
                first.End = 2019;
            }
            var single = elected.SingleOrDefault(x => x.DivisionNm == fullName);
            if (single != null)
            {
                electorateMembers.Insert(0,
                    new Member
                    {
                        GivenNames = single.GivenNm,
                        FamilyName = single.Surname.ToTitleCase(),
                        Begin = 2019,
                        Party = single.PartyAb
                    });
            }

            electorate.Members = electorateMembers;
            electorate.DemographicRating = values["Demographic Rating"].TrimmedInnerHtml();
            electorate.ProductsAndIndustry = values["Products/Industries of the Area"].TrimmedInnerHtml();
            electorate.NameDerivation = values["Name derivation"].TrimmedInnerHtml();
            if (values.TryGetValue("Location Description", out var description))
            {
                electorate.Description = description.TrimmedInnerHtml();
            }

            if (values.TryGetValue("Area and Location Description", out description))
            {
                electorate.Description = description.TrimmedInnerHtml();
            }

            if (values.TryGetValue("Area", out var area))
            {
                electorate.Area = double.Parse(area.InnerHtml.Trim().Replace("&nbsp;", " ").Replace(" ", "").Replace("sqkm", "").Replace(",", ""));
            }

            return electorate;
        }
        catch (Exception exception)
        {
            throw new Exception($"Failed to parse {shortName} {tempElectorateHtmlPath}", exception);
        }
    }

    static string GetFullName(HtmlDocument document, string prefix)
    {
        var headings = document.Headings();
        var caseless = headings.Single(x => x.StartsWith(prefix))
            .ReplaceCaseless(prefix, "");
        return TrimState(caseless);
    }

    static string TrimState(string caseless)
    {
        var strings = caseless
            .Split(new[] {" ("}, StringSplitOptions.None);
        var fullName = strings[0];
        Assert.NotEmpty(fullName);
        return fullName;
    }

    static IEnumerable<Member> ElectorateMembers(Dictionary<string, HtmlNode> values)
    {
        var members = values["Members"];
        if (members.InnerText.Contains("will be elected at the next federal general election."))
        {
            yield break;
        }

        var texts = members
            .Descendants("li")
            .Select(x => x.ChildNodes.First().InnerText)
            .ToList();
        if (texts.Count == 0)
        {
            texts = members.ChildNodes
                .Where(x => x.NodeType == HtmlNodeType.Text)
                .Select(x => x.InnerText)
                .ToList();
        }

        foreach (var text in texts)
        {
            var cleaned = text.TrimEnd('(').TrimmedInnerHtml();
            if (cleaned.Length == 0)
            {
                continue;
            }

            var split = cleaned.Split(new[] {" ("}, 2, StringSplitOptions.None);
            var member = split[0];
            split = split[1].Split(new[] {") "}, 2, StringSplitOptions.None);
            var party = split[0];

            split = split[1].Split(new[] {"-"}, 2, StringSplitOptions.RemoveEmptyEntries);
            var begin = ushort.Parse(split[0].Trim());
            ushort? end = null;
            if (split.Length > 1)
            {
                end = ushort.Parse(split[1].Trim());
            }

            var (familyName, givenNames) = SplitName(member);
            yield return new Member
            {
                FamilyName = familyName,
                GivenNames = givenNames,
                Party = party,
                Begin = begin,
                End = end,
            };
        }
    }

    static (string familyName, string givenNames) SplitName(string member)
    {
        var memberSplit = member.Split(',');
        var familyName = memberSplit[0].ToTitleCase();
        var givenNames = memberSplit[1].Trim();
        return (familyName, givenNames);
    }

    static HtmlNode FindProfileTable(HtmlDocument document)
    {
        return document.DocumentNode.SelectSingleNode("//comment()[contains(., ' InstanceBeginEditable name=\"Content\" ')]/following-sibling::dl");
    }
}