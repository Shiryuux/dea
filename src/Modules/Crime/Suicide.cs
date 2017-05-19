﻿using DEA.Common.Data;
using DEA.Common.Extensions;
using Discord.Commands;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace DEA.Modules.Crime
{
    public partial class Crime
    {
        [Command("Suicide")]
        [Alias("kms", "commitsuicide")]
        [Summary("Kill yourself.")]
        public async Task Suicide()
        {
            if (Context.Cash < Config.SUICIDE_COST)
            {
                ReplyError($"You need {Config.SUICIDE_COST.USD()} to be able to buy a high quality noose. Balance: {Context.Cash.USD()}.");
            }

            await _userRepo.Collection.DeleteOneAsync(x => x.UserId == Context.User.Id && x.GuildId == Context.Guild.Id);

            await ReplyAsync($"You have successfully killed yourself.");
        }
    }
}