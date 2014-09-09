SEKingOfTheHill

===============



King Of The Hill game mode plugin for Space Engineers Server Extender.

The way I imagined it while creating it was having two or more factions basically engaging in an arms race. For example, the red faction captures the beacon and builds a small defense platform near it, so the blue faction builds a ship that can defeat that defense platform, and captures the beacon. Red builds something that can defeat Blue's ship, and so on.
It was intended to be played on creative, with the idea that anyone could join in at any time, build their ship and try and capture the beacon. There's a new round evey x seconds (default is 600, though on survival I'd recommend maybe an hour instead), so even if there are already established factions, there's opportunity for a newcomer to come in and win the round.
I've added as few extraneous features as possible, so that server administrators can set it up to their liking. You could for example, play it on survival mode, and replace the spawn ships with small fighters and other attack crafts, then when somebody secures the beacon beyond the abilities of those crafts, an arms race starts with people setting up bases and stuff.
The idea was to just give people a goal to work towards, beyond simply demonstrating their creativity.

Configuration can only be done through the SE Server Extender's plugin window. Through this, you can configure the position of the hill and the length of the rounds.

Chat commands
===============
/leaderboard - shows the current top 5 factions and their scores.

/kothenable - enables the plugin if it has been disabled.

/kothdisable - disables the plugin if it is enabled.

/kothreset - resets all scores and restarts the rounds.

/kothcleanup - Ideally this shouldn't need to be used, but if for some reason the hill has been duplicated, this forces the hill to be destroyed and rebuilt.
