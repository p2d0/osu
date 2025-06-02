﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online
{
    public class DevelopmentEndpointConfiguration : EndpointConfiguration
    {
        public DevelopmentEndpointConfiguration()
        {
            WebsiteUrl = APIUrl = @"http://localhost:8080";
            APIClientSecret = @"5nKGpRnRebh0P8Pq5RbbiGga0XBaBPWbu9SQldpZ";
            APIClientID = "1";
            SpectatorUrl = $@"{APIUrl}/signalr/spectator";
            MultiplayerUrl = $@"{APIUrl}/signalr/multiplayer";
            MetadataUrl = $@"{APIUrl}/signalr/metadata";
            BeatmapSubmissionServiceUrl = $@"{APIUrl}/beatmap-submission";
        }
    }
}
