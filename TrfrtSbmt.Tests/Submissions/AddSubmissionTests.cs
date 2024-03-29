﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using TrfrtSbmt.Api.Features.Submissions;
using System;
using static TrfrtSbmt.Tests.TestFixture;
using System.Threading.Tasks;
using TrfrtSbmt.Api.Features.Festivals;
using CodenameGenerator;
using TrfrtSbmt.Api.Features.Forts;
using System.Linq;
using Shouldly;

namespace TrfrtSbmt.Tests.Submissions;

[TestClass]
public partial class AddSubmissionTests
{
    FestivalViewModel festival;
    FortViewModel fort;

    [TestInitialize]
    public async Task Initialize()
    {
        NameGenerator.EndsWith = $" Festival 20{Rand.Next(10, 30)}!";
        var addFestivalCommand = new AddFestivalCommand(true, NameGenerator.Generate(), Lorem, DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow.AddMonths(3));
        festival = await SendAsync(addFestivalCommand);

        NameGenerator.SetParts(WordBank.Nouns);
        NameGenerator.EndsWith = $"Fort";
        var command = new AddFort.AddFortCommand("Music", Lorem)
        {
            FestivalId = festival.Id
        };
        fort = await SendAsync(command);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        var deleteFest1 = new DeleteFestivalCommand(festival.Id);
        await SendAsync(deleteFest1);

        NameGenerator.SetParts(WordBank.Verbs, WordBank.Nouns);
        NameGenerator.EndsWith = string.Empty;
    }

    [TestMethod]
    public async Task SmokeTest() 
    {
        for (int i = 0; i < 10; i++)
        {
            // arrange
            var command = new AddSubmission.AddSubmissionCommand(GeneratorMethods.GenerateName(), GeneratorMethods.GenerateState(), GeneratorMethods.GenerateCity(), GeneratorMethods.GenerateCountry(), Lorem, GeneratorMethods.GeneratePictureUrl(), "https://www.reddit.com/r/U2Band/", GeneratorMethods.GenerateGenre().Distinct(), Lorem, new SocialLinksVm("https://open.spotify.com/artist/51Blml2LZPmy7TTiAg47vQ", "https://music.apple.com/us/artist/u2/78500", "https://suckling.bandcamp.com/releases", "https://soundcloud.com/u2", new string[3] { "https://www.youtube.com/watch?v=ujNeHIo7oTE&ab_channel=U2VEVO", "https://www.youtube.com/watch?v=98W9QuMq-2k&ab_channel=U2VEVO", "https://www.youtube.com/watch?v=co6WMzDOh1o&ab_channel=U2VEVO" }, "https://www.facebook.com/u2", "https://twitter.com/u2", "https://www.instagram.com/u2", "https://www.tiktok.com/@u2?lang=en"), GeneratorMethods.GenerateContactInfo())
            {
                FestivalId = festival.Id,
                FortId = fort.Id
            };

            // act
            await SendAsync(command);

            // assert
            //submission.Id.IsNotNull();
        }
        
    }

    [TestMethod]
    public async Task Nulls()
    {
        for (int i = 0; i < 10; i++)
        {
            // arrange
            var command = new AddSubmission.AddSubmissionCommand(
                "a", "a","a","A","a", "A", "A", new string[0], "A",  new SocialLinksVm(string.Empty, string.Empty, string.Empty, string.Empty, new string[0], string.Empty, string.Empty, string.Empty, string.Empty), new ContactInfoVm("A", "A", "smith.colin00@gmail.com", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty)
                )
            {
                FestivalId = festival.Id,
                FortId = fort.Id
            };

            // act
            await SendAsync(command);

            // assert
            //submission.Id.IsNotNull();
        }

    }

    [TestMethod]
    public async Task Update()
    {
        // arrange
        var command = new AddSubmission.AddSubmissionCommand(GeneratorMethods.GenerateName(), GeneratorMethods.GenerateState(), GeneratorMethods.GenerateCity(), GeneratorMethods.GenerateCountry(), Lorem, GeneratorMethods.GeneratePictureUrl(), "https://www.reddit.com/r/U2Band/", GeneratorMethods.GenerateGenre().Distinct(), Lorem, new SocialLinksVm("https://open.spotify.com/artist/51Blml2LZPmy7TTiAg47vQ", "https://music.apple.com/us/artist/u2/78500", "https://suckling.bandcamp.com/releases", "https://soundcloud.com/u2", new string[3] { "https://www.youtube.com/watch?v=ujNeHIo7oTE&ab_channel=U2VEVO", "https://www.youtube.com/watch?v=98W9QuMq-2k&ab_channel=U2VEVO", "https://www.youtube.com/watch?v=co6WMzDOh1o&ab_channel=U2VEVO" }, "https://www.facebook.com/u2", "https://twitter.com/u2", "https://www.instagram.com/u2", "https://www.tiktok.com/@u2?lang=en"), GeneratorMethods.GenerateContactInfo())
        {
            FestivalId = festival.Id,
            FortId = fort.Id
        };
        var sub = await SendAsync(command);

        var update = new AddSubmission.AddSubmissionCommand("POOP", GeneratorMethods.GenerateState(), GeneratorMethods.GenerateCity(), GeneratorMethods.GenerateCountry(), Lorem, GeneratorMethods.GeneratePictureUrl(), "https://www.reddit.com/r/U2Band/", GeneratorMethods.GenerateGenre().Distinct(), Lorem, new SocialLinksVm("https://open.spotify.com/artist/51Blml2LZPmy7TTiAg47vQ", "https://music.apple.com/us/artist/u2/78500", "https://suckling.bandcamp.com/releases", "https://soundcloud.com/u2", new string[3] { "https://www.youtube.com/watch?v=ujNeHIo7oTE&ab_channel=U2VEVO", "https://www.youtube.com/watch?v=98W9QuMq-2k&ab_channel=U2VEVO", "https://www.youtube.com/watch?v=co6WMzDOh1o&ab_channel=U2VEVO" }, "https://www.facebook.com/u2", "https://twitter.com/u2", "https://www.instagram.com/u2", "https://www.tiktok.com/@u2?lang=en"), GeneratorMethods.GenerateContactInfo(), sub.Id)
        {
            FestivalId = festival.Id,
            FortId = fort.Id
        };

        // act
        var updatedSub = await SendAsync(update);

        // assert
        updatedSub.Name.ShouldBe("POOP");
    }
}
