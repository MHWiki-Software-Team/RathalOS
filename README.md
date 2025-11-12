# RathalOS
Task Tracker bot for the independent [Monster Hunter Wiki](https://monsterhunterwiki.org/wiki/Main_Page).


## Build Information
The bot uses [Discord.Net](https://docs.discordnet.dev/) as its Discord API wrapper. Follow [this guide](https://docs.discordnet.dev/guides/introduction/intro.html) for setting up a bot with the [Discord Developer Portal](https://discord.com/developers/applications).

Once you have your bot set up, fill in the values in the [App.config.template](https://github.com/MHWiki-Software-Team/RathalOS/blob/main/App.config.template) and rename it to App.config in your solution.

This bot is containerized through Docker. This means that, to run the bot, you'll need Docker tools installed on your machine. If you're using Visual Studio, you can just install [Docker Desktop](https://www.docker.com/products/docker-desktop/) and run it. Your solution will attach to your Docker Desktop installation and take care of the rest.

You'll also need to either have a SQL Server to attach the bot DB to, or you'll have to change the storage solution in the [Wiki_DbContext.cs](https://github.com/MHWiki-Software-Team/RathalOS/blob/main/Data/Wiki_DbContext.cs) to refer to whatever storage solution you'd prefer to use.
