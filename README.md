# TwitchChannelPoints

Inspired by https://github.com/gottagofaster236/Twitch-Channel-Points-Miner

Program is a lot bigger than necessary because I used .NET Core and bundled the runtime instead of .NET Framework, too lazy to change.
This was made for myself, and it needs a Twitch OAuth cookie from "auth-token".
The program can watch a max of 2 streamers at a time since this is a Twitch limitation. It prioritizes by who is at the top of the list.

Gets points from
- Watching 5 minutes
- Claim special bonuses
- Grow a watch streak (Prioritizes a new stream over priority list, to grow watch streaks for streamers not at the top of the list)

Doesn't support raids, and I don't plan on adding support for it.

![](https://i.imgur.com/LU2vhC6.png)
